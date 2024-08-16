// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

internal sealed class ExtractToComponentCodeActionProvider(ILoggerFactory loggerFactory) : IRazorCodeActionProvider
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<ExtractToComponentCodeActionProvider>();

    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        if (!IsValidContext(context))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        if (!IsSelectionValid(context, syntaxTree))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!TryGetNamespace(context.CodeDocument, out var @namespace))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var actionParams = CreateInitialActionParams(context, startElementNode, @namespace);        

        ProcessSelection(startElementNode, endElementNode, actionParams);

        var utilityScanRoot = FindNearestCommonAncestor(startElementNode, endElementNode) ?? startElementNode;

        // The new component usings are going to be a subset of the usings in the source razor file.
        var usingStrings = syntaxTree.Root.DescendantNodes().Where(node => node.IsUsingDirective(out var _)).Select(node => node.ToFullString().TrimEnd());

        // Get only the namespace after the "using" keyword.
        var usingNamespaceStrings = usingStrings.Select(usingString => usingString.Substring("using  ".Length));

        AddUsingDirectivesInRange(utilityScanRoot,
                                  usingNamespaceStrings,
                                  actionParams.ExtractStart,
                                  actionParams.ExtractEnd,
                                  actionParams);

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            Action = LanguageServerConstants.CodeActions.ExtractToNewComponentAction,
            Language = LanguageServerConstants.CodeActions.Languages.Razor,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToComponent(resolutionParams);
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    private static bool IsValidContext(RazorCodeActionContext context)
    {
        return context is not null &&
               context.SupportsFileCreation &&
               FileKinds.IsComponent(context.CodeDocument.GetFileKind()) &&
               context.CodeDocument.GetSyntaxTree()?.Root is not null;
    }

    private static bool IsSelectionValid(RazorCodeActionContext context, RazorSyntaxTree syntaxTree)
    {
        var owner = syntaxTree.Root.FindInnermostNode(context.Location.AbsoluteIndex, includeWhitespace: true);
        var startElementNode = owner?.FirstAncestorOrSelf<MarkupElementSyntax>();
        return startElementNode is not null && !IsInsideProperHtmlContent(context, startElementNode) && !HasDiagnosticErrors(startElementNode);
    }

    private static bool IsInsideProperHtmlContent(RazorCodeActionContext context, MarkupElementSyntax startElementNode)
    {
        // If the provider executes before the user/completion inserts an end tag, the below return fails
        if (startElementNode.EndTag.IsMissing)
        {
            return true;
        }

        return context.Location.AbsoluteIndex > startElementNode.StartTag.Span.End &&
               context.Location.AbsoluteIndex < startElementNode.EndTag.SpanStart;
    }

    private static bool HasDiagnosticErrors(MarkupElementSyntax markupElement)
    {
        var diagnostics = markupElement.GetDiagnostics();
        return diagnostics.Any(d => d.Severity == RazorDiagnosticSeverity.Error);
    }

    private static bool TryGetNamespace(RazorCodeDocument codeDocument, [NotNullWhen(returnValue: true)] out string? @namespace)
        // If the compiler can't provide a computed namespace it will fallback to "__GeneratedComponent" or
        // similar for the NamespaceNode. This would end up with extracting to a wrong namespace
        // and causing compiler errors. Avoid offering this refactoring if we can't accurately get a
        // good namespace to extract to
        => codeDocument.TryComputeNamespace(fallbackToRootNamespace: true, out @namespace);
}
