// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

using Microsoft.AspNetCore.Razor.Language.Syntax;

internal static class RazorSyntaxTreeExtensions
{
    {
        if (syntaxTree is null)
        {
            throw new ArgumentNullException(nameof(syntaxTree));
        }

    public static ImmutableArray<RazorDirectiveSyntax> GetCodeBlockDirectives(this RazorSyntaxTree syntaxTree)
        // We want all nodes of type RazorDirectiveSyntax which will contain code.
        // Since code block directives occur at the top-level, we don't need to dive deeper into unrelated nodes.
        var codeBlockDirectives = syntaxTree.Root
            .DescendantNodes(node => node is RazorDocumentSyntax || node is MarkupBlockSyntax || node is CSharpCodeBlockSyntax)
            .OfType<RazorDirectiveSyntax>()
            .Where(directive => directive.DirectiveDescriptor?.Kind == DirectiveKind.CodeBlock)
            .SelectAsArray(d => d);

        return codeBlockDirectives;
    }

    public static IReadOnlyList<CSharpStatementSyntax> GetCSharpStatements(this RazorSyntaxTree syntaxTree)
    {
        // We want all nodes that represent Razor C# statements, @{ ... }.
        var statements = syntaxTree.Root.DescendantNodes().OfType<CSharpStatementSyntax>().ToList();
        return statements;
    }

    public static SyntaxNode? FindInnermostNode(
        this RazorSyntaxTree syntaxTree,
        SourceText sourceText,
        Position position,
        ILogger logger,
        bool includeWhitespace = false)
    {
        if (!position.TryGetAbsoluteIndex(sourceText, logger, out var absoluteIndex))
        {
            return default;
        }

        return syntaxTree.Root.FindInnermostNode(absoluteIndex, includeWhitespace);
    }
}
