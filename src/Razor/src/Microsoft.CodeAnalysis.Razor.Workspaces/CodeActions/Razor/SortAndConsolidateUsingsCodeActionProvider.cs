// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

internal class SortAndConsolidateUsingsCodeActionProvider : IRazorCodeActionProvider
{
    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        if (context.HasSelection)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!context.CodeDocument.TryGetSyntaxRoot(out var root))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Only offer when cursor is on a using directive
        var owner = root.FindNode(TextSpan.FromBounds(context.StartAbsoluteIndex, context.EndAbsoluteIndex));
        if (owner?.FirstAncestorOrSelf<RazorUsingDirectiveSyntax>() is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!UsingDirectiveHelper.TryGetSortedAndConsolidatedNamespaces(context.CodeDocument, out _))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            TextDocument = context.Request.TextDocument,
            Action = LanguageServerConstants.CodeActions.SortAndConsolidateUsings,
            Language = RazorLanguageKind.Razor,
            DelegatedDocumentUri = context.DelegatedDocumentUri,
            Data = true
        };

        var codeAction = RazorCodeActionFactory.CreateSortAndConsolidateUsings(resolutionParams);
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }
}
