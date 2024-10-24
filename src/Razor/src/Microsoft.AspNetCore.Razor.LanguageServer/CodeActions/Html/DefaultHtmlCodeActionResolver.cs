// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal sealed class DefaultHtmlCodeActionResolver(
    IDelegatedCodeActionResolver delegatedCodeActionResolver,
    IEditMappingService editMappingService) : IHtmlCodeActionResolver
{
    private readonly IDelegatedCodeActionResolver _delegatedCodeActionResolver = delegatedCodeActionResolver;
    private readonly IEditMappingService _editMappingService = editMappingService;

    public string Action => LanguageServerConstants.CodeActions.Default;

    public async Task<CodeAction> ResolveAsync(
        DocumentContext documentContext,
        CodeAction codeAction,
        CancellationToken cancellationToken)
    {
        var resolvedCodeAction = await _delegatedCodeActionResolver.ResolveCodeActionAsync(documentContext.GetTextDocumentIdentifier(), documentContext.Snapshot.Version, RazorLanguageKind.Html, codeAction, cancellationToken).ConfigureAwait(false);
        if (resolvedCodeAction?.Edit?.DocumentChanges is null)
        {
            // Unable to resolve code action with server, return original code action
            return codeAction;
        }

        await DefaultHtmlCodeActionProvider.RemapAndFixHtmlCodeActionEditAsync(_editMappingService, documentContext.Snapshot, resolvedCodeAction, cancellationToken).ConfigureAwait(false);

        return resolvedCodeAction;
    }
}
