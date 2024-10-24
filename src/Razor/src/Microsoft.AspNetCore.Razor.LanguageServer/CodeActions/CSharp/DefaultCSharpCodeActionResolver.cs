// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal sealed class DefaultCSharpCodeActionResolver(
    IDelegatedCodeActionResolver delegatedCodeActionResolver,
    IRazorFormattingService razorFormattingService) : ICSharpCodeActionResolver
{
    private readonly IDelegatedCodeActionResolver _delegatedCodeActionResolver = delegatedCodeActionResolver;
    private readonly IRazorFormattingService _razorFormattingService = razorFormattingService;

    public string Action => LanguageServerConstants.CodeActions.Default;

    public async Task<CodeAction> ResolveAsync(
        DocumentContext documentContext,
        CodeAction codeAction,
        CancellationToken cancellationToken)
    {
        var resolvedCodeAction = await _delegatedCodeActionResolver.ResolveCodeActionAsync(documentContext.GetTextDocumentIdentifier(), documentContext.Snapshot.Version, RazorLanguageKind.CSharp, codeAction, cancellationToken).ConfigureAwait(false);
        if (resolvedCodeAction?.Edit?.DocumentChanges is null)
        {
            // Unable to resolve code action with server, return original code action
            return codeAction;
        }

        if (resolvedCodeAction.Edit.DocumentChanges.Value.Count() != 1)
        {
            // We don't yet support multi-document code actions, return original code action
            return codeAction;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var documentChanged = resolvedCodeAction.Edit.DocumentChanges.Value.First();
        if (!documentChanged.TryGetFirst(out var textDocumentEdit))
        {
            // Only Text Document Edit changes are supported currently, return original code action
            return codeAction;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var csharpSourceText = await documentContext.GetCSharpSourceTextAsync(cancellationToken).ConfigureAwait(false);
        var csharpTextChanges = textDocumentEdit.Edits.SelectAsArray(csharpSourceText.GetTextChange);

        // Remaps the text edits from the generated C# to the razor file,
        // as well as applying appropriate formatting.
        var formattedChange = await _razorFormattingService.TryGetCSharpCodeActionEditAsync(
            documentContext,
            csharpTextChanges,
            new RazorFormattingOptions(),
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        var codeDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier()
        {
            Uri = documentContext.Uri
        };
        resolvedCodeAction.Edit = new WorkspaceEdit()
        {
            DocumentChanges = new TextDocumentEdit[] {
                new TextDocumentEdit()
                {
                    TextDocument = codeDocumentIdentifier,
                    Edits = formattedChange is { } change ? [sourceText.GetTextEdit(change)] : [],
                }
            }
        };

        return resolvedCodeAction;
    }
}
