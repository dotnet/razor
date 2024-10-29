// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using RazorSyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

internal static class AddUsingsHelper
{
    private static readonly Regex s_addUsingVSCodeAction = new Regex("@?using ([^;]+);?$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private readonly record struct RazorUsingDirective(RazorDirectiveSyntax Node, AddImportChunkGenerator Statement);

    public static async Task<TextEdit[]> GetUsingStatementEditsAsync(RazorCodeDocument codeDocument, SourceText originalCSharpText, SourceText changedCSharpText, CancellationToken cancellationToken)
    {
        // Now that we're done with everything, lets see if there are any using statements to fix up
        // We do this by comparing the original generated C# code, and the changed C# code, and look for a difference
        // in using statements. We can't use edits for this for two main reasons:
        //
        // 1. Using statements in the generated code might come from _Imports.razor, or from this file, and C# will shove them anywhere
        // 2. The edit might not be clean. eg given:
        //      using System;
        //      using System.Text;
        //    Adding "using System.Linq;" could result in an insert of "Linq;\r\nusing System." on line 2
        //
        // So because of the above, we look for a difference in C# using directive nodes directly from the C# syntax tree, and apply them manually
        // to the Razor document.

        var originalSyntaxTree = CSharpSyntaxTree.ParseText(originalCSharpText, cancellationToken: cancellationToken);
        var changedSyntaxTree = originalSyntaxTree.WithChangedText(changedCSharpText);
        var oldUsings = await FindUsingDirectiveStringsAsync(originalSyntaxTree, cancellationToken).ConfigureAwait(false);
        var newUsings = await FindUsingDirectiveStringsAsync(changedSyntaxTree, cancellationToken).ConfigureAwait(false);

        using var edits = new PooledArrayBuilder<TextEdit>();
        var addedUsings = Delta.Compute(oldUsings, newUsings);
        return GenerateUsingsEdits(codeDocument, newUsings);
    }

    /// <summary>
    /// Extracts the namespace from a C# add using statement provided by Visual Studio
    /// </summary>
    /// <param name="csharpAddUsing">Add using statement of the form `using System.X;`</param>
    /// <param name="namespace">Extract namespace `System.X`</param>
    /// <param name="prefix">The prefix to show, before the namespace, if any</param>
    /// <returns></returns>
    public static bool TryExtractNamespace(string csharpAddUsing, out string @namespace, out string prefix)
    {
        // We must remove any leading/trailing new lines from the add using edit
        csharpAddUsing = csharpAddUsing.Trim();
        var regexMatchedTextEdit = s_addUsingVSCodeAction.Match(csharpAddUsing);
        if (!regexMatchedTextEdit.Success ||

            // Two Regex matching groups are expected
            // 1. `using namespace;`
            // 2. `namespace`
            regexMatchedTextEdit.Groups.Count != 2)
        {
            // Text edit in an unexpected format
            @namespace = string.Empty;
            prefix = string.Empty;
            return false;
        }

        @namespace = regexMatchedTextEdit.Groups[1].Value;
        prefix = csharpAddUsing[..regexMatchedTextEdit.Index];
        return true;
    }

    public static async Task<ImmutableArray<string>> FindUsingDirectiveStringsAsync(SyntaxTree syntaxTree, CancellationToken cancellationToken)
    {
        var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

        // We descend any compilation unit (ie, the file) or and namespaces because the compiler puts all usings inside
        // the namespace node.
        var usings = syntaxRoot.DescendantNodes(n => n is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
            // Filter to using directives
            .OfType<UsingDirectiveSyntax>()
            // Select everything after the initial "using " part of the statement, and excluding the ending semi-colon. The
            // semi-colon is valid in Razor, but users find it surprising. This is slightly lazy, for sure, but has
            // the advantage of us not caring about changes to C# syntax, we just grab whatever Roslyn wanted to put in, so
            // we should still work in C# v26
            .Select(u => u.ToString()["using ".Length..^1]);

        return usings.ToImmutableArray();
    }

    public static TextEdit[] GenerateUsingsEdits(
        RazorCodeDocument codeDocument,
        ImmutableArray<string> newUsingNamespaces)
    {
        var systemUsings = newUsingNamespaces.Where(ns => ns.StartsWith("System", StringComparison.Ordinal)).OrderByAsArray(s => s, StringComparer.Ordinal);
        var otherUsings = Delta.Compute(systemUsings, newUsingNamespaces).Sort(StringComparer.Ordinal);

        var remainingSystemUsings = systemUsings.AsSpan();
        var remainingOtherUsings = otherUsings.AsSpan();

        // Tracks where the using should be inserted by absolute path in the document. Should always
        // be incremented when a new using is added.
        var insertAbsolutePosition = 0;

        using var edits = new PooledArrayBuilder<TextEdit>();

        foreach (var usingDirective in CollectUsingDirectives(codeDocument))
        {
            var usingDirectiveNamespace = usingDirective.Statement.ParsedNamespace;

            // Group system usings together
            if (usingDirectiveNamespace.StartsWith("System", StringComparison.Ordinal))
            {
                if (remainingSystemUsings.Length == 0)
                {
                    continue;
                }

                if (string.CompareOrdinal(usingDirectiveNamespace, remainingSystemUsings[0]) < 0)
                {
                    insertAbsolutePosition += AddUsingsAfter(usingDirective.Node.Span.End, remainingSystemUsings[0]);
                    remainingSystemUsings = remainingSystemUsings[1..];
                    continue;
                }
            }

            // No more existing system usings, add the remaining system usings
            insertAbsolutePosition += AddAllUsingsAfter(insertAbsolutePosition, remainingSystemUsings);
            remainingSystemUsings = Span<string>.Empty;

            if (remainingOtherUsings.Length == 0)
            {
                break;
            }

            if (string.CompareOrdinal(usingDirectiveNamespace, remainingOtherUsings[0]) < 0)
            {
                insertAbsolutePosition += AddUsingsAfter(usingDirective.Node.Span.End, remainingOtherUsings[0]);
                remainingOtherUsings = remainingOtherUsings[1..];
            }
        }

        // Add remaining usings with system usings being first
        Debug.Assert(insertAbsolutePosition == 0 || remainingSystemUsings.IsEmpty, "System usings should have been added before other usings.");
        insertAbsolutePosition += AddAllUsingsAfter(insertAbsolutePosition, remainingSystemUsings);
        insertAbsolutePosition += AddAllUsingsAfter(insertAbsolutePosition, remainingOtherUsings);

        return edits.ToArray();

        int AddUsingsAfter(int absolutePosition, string newNamespace)
        {
            var linePosition = GetUsingInsertionPosition(codeDocument, absolutePosition);
            var newText = GenerateUsingDirectiveText(newNamespace);
            var edit = VsLspFactory.CreateTextEdit(line: linePosition.Line, character: linePosition.Character, newText);
            edits.Add(edit);

            return newText.Length;
        }

        int AddAllUsingsAfter(int absolutePosition, ReadOnlySpan<string> newNamspaces)
        {
            if (newNamspaces.IsEmpty)
            {
                return 0;
            }

            using var _ = StringBuilderPool.GetPooledObject(out var builder);
            foreach (var newNamespace in newNamspaces)
            {
                builder.Append(GenerateUsingDirectiveText(newNamespace));
            }

            var linePosition = GetUsingInsertionPosition(codeDocument, absolutePosition);
            var edit = VsLspFactory.CreateTextEdit(line: linePosition.Line, character: linePosition.Character, builder.ToString());
            edits.Add(edit);

            return builder.Length;
        }
    }

    private static LinePosition GetUsingInsertionPosition(RazorCodeDocument codeDocument, int absolutePosition)
    {
        if (absolutePosition == 0)
        {
            var lastNamespaceOrPageDirective = codeDocument
                .GetSyntaxTree()
                .Root
                .DescendantNodes()
                .LastOrDefault(IsNamespaceOrPageDirective);

            if (lastNamespaceOrPageDirective is not null)
            {
                var lineIndex = GetLineIndexOrEnd(codeDocument, lastNamespaceOrPageDirective.Span.End - 1);
                return new LinePosition(lineIndex + 1, 0);
            }

            return LinePosition.Zero;
        }

        var line = codeDocument.Source.Text.Lines.GetLineFromPosition(absolutePosition);
        return new LinePosition(
            line.LineNumber + 1,
            0);

        static bool IsNamespaceOrPageDirective(RazorSyntaxNode node)
        {
            if (node is RazorDirectiveSyntax directiveNode)
            {
                return directiveNode.DirectiveDescriptor == ComponentPageDirective.Directive ||
                    directiveNode.DirectiveDescriptor == NamespaceDirective.Directive ||
                    directiveNode.DirectiveDescriptor == PageDirective.Directive;
            }

            return false;
        }
    }

    private static string GenerateUsingDirectiveText(string newUsingNamespace)
        => $"@using {newUsingNamespace}{Environment.NewLine}";

    private static int GetLineIndexOrEnd(RazorCodeDocument codeDocument, int endIndex)
    {
        if (endIndex < codeDocument.Source.Text.Length)
        {
            return codeDocument.Source.Text.GetLinePosition(endIndex).Line;
        }
        else
        {
            return codeDocument.Source.Text.Lines.Count;
        }
    }

    private static ImmutableArray<RazorUsingDirective> CollectUsingDirectives(RazorCodeDocument codeDocument)
    {
        using var usingDirectives = new PooledArrayBuilder<RazorUsingDirective>();

        var syntaxTreeRoot = codeDocument.GetSyntaxTree().Root;
        foreach (var node in syntaxTreeRoot.DescendantNodes())
        {
            if (node is RazorDirectiveSyntax directiveNode)
            {
                foreach (var child in directiveNode.DescendantNodes())
                {
                    if (child.GetChunkGenerator() is AddImportChunkGenerator { IsStatic: false } usingStatement)
                    {
                        usingDirectives.Add(new RazorUsingDirective(directiveNode, usingStatement));
                    }
                }
            }
        }

        return usingDirectives.ToImmutable();
    }
}
