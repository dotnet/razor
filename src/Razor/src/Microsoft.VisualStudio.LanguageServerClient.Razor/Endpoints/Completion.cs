// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Editor.Razor.Snippets;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using static Microsoft.VisualStudio.Editor.Razor.Snippets.XmlSnippetParser;
using RazorTextSpan = Microsoft.AspNetCore.Razor.Language.Syntax.TextSpan;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    // Called by the Razor Language Server to provide inline completions from the platform.
    [JsonRpcMethod(CustomMessageNames.RazorInlineCompletionEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task<VSInternalInlineCompletionList?> ProvideInlineCompletionAsync(RazorInlineCompletionRequest inlineCompletionParams, CancellationToken cancellationToken)
    {
        if (inlineCompletionParams is null)
        {
            throw new ArgumentNullException(nameof(inlineCompletionParams));
        }

        var hostDocumentUri = inlineCompletionParams.TextDocument.Uri;
        if (!_documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot))
        {
            return null;
        }

        // TODO: Support multiple C# documents per Razor document.
        if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
        {
            return null;
        }

        var csharpRequest = new VSInternalInlineCompletionRequest
        {
            Context = inlineCompletionParams.Context,
            Position = inlineCompletionParams.Position,
            TextDocument = inlineCompletionParams.TextDocument.WithUri(csharpDoc.Uri),
            Options = inlineCompletionParams.Options,
        };

        var textBuffer = csharpDoc.Snapshot.TextBuffer;
        var request = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalInlineCompletionRequest, VSInternalInlineCompletionList?>(
            textBuffer,
            VSInternalMethods.TextDocumentInlineCompletionName,
            RazorLSPConstants.RazorCSharpLanguageServerName,
            csharpRequest,
            cancellationToken).ConfigureAwait(false);

        return request?.Response;
    }

    // JToken returning because there's no value in converting the type into its final type because this method serves entirely as a delegation point (immediately re-serializes).
    [JsonRpcMethod(LanguageServerConstants.RazorCompletionEndpointName, UseSingleObjectParameterDeserialization = true)]
    public async Task<JToken?> ProvideCompletionsAsync(
        DelegatedCompletionParams request,
        CancellationToken cancellationToken)
    {
        var hostDocumentUri = request.Identifier.TextDocumentIdentifier.Uri;

        string languageServerName;
        Uri projectedUri;
        bool synchronized;
        VirtualDocumentSnapshot virtualDocumentSnapshot;
        if (request.ProjectedKind == RazorLanguageKind.Html)
        {
            (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
            projectedUri = virtualDocumentSnapshot.Uri;
        }
        else if (request.ProjectedKind == RazorLanguageKind.CSharp)
        {
            (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
            projectedUri = virtualDocumentSnapshot.Uri;
        }
        else
        {
            Debug.Fail("Unexpected RazorLanguageKind. This shouldn't really happen in a real scenario.");
            return null;
        }

        if (!synchronized)
        {
            return null;
        }

        var completionParams = new CompletionParams()
        {
            Context = request.Context,
            Position = request.ProjectedPosition,
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(projectedUri),
        };

        var continueOnCapturedContext = false;
        var provisionalTextEdit = request.ProvisionalTextEdit;
        if (provisionalTextEdit is not null)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var provisionalChange = new VisualStudioTextChange(provisionalTextEdit, virtualDocumentSnapshot.Snapshot);
            UpdateVirtualDocument(provisionalChange, request.ProjectedKind, request.Identifier.Version, hostDocumentUri, virtualDocumentSnapshot.Uri);

            // We want the delegation to continue on the captured context because we're currently on the `main` thread and we need to get back to the
            // main thread in order to update the virtual buffer with the reverted text edit.
            continueOnCapturedContext = true;
        }

        try
        {
            var textBuffer = virtualDocumentSnapshot.Snapshot.TextBuffer;
            var lspMethodName = Methods.TextDocumentCompletion.Name;
            using var _ = _telemetryReporter.TrackLspRequest(lspMethodName, languageServerName, request.CorrelationId);
            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<CompletionParams, JToken?>(
                textBuffer,
                lspMethodName,
                languageServerName,
                completionParams,
                cancellationToken).ConfigureAwait(continueOnCapturedContext);
            return response?.Response;
        }
        finally
        {
            if (provisionalTextEdit is not null)
            {
                var revertedProvisionalTextEdit = BuildRevertedEdit(provisionalTextEdit);
                var revertedProvisionalChange = new VisualStudioTextChange(revertedProvisionalTextEdit, virtualDocumentSnapshot.Snapshot);
                UpdateVirtualDocument(revertedProvisionalChange, request.ProjectedKind, request.Identifier.Version, hostDocumentUri, virtualDocumentSnapshot.Uri);
            }
        }
    }

    private static TextEdit BuildRevertedEdit(TextEdit provisionalTextEdit)
    {
        TextEdit? revertedProvisionalTextEdit;
        if (provisionalTextEdit.Range.Start == provisionalTextEdit.Range.End)
        {
            // Insertion
            revertedProvisionalTextEdit = new TextEdit()
            {
                Range = new Range()
                {
                    Start = provisionalTextEdit.Range.Start,

                    // We're making an assumption that provisional text edits do not span more than 1 line.
                    End = new Position(provisionalTextEdit.Range.End.Line, provisionalTextEdit.Range.End.Character + provisionalTextEdit.NewText.Length),
                },
                NewText = string.Empty
            };
        }
        else
        {
            // Replace
            revertedProvisionalTextEdit = new TextEdit()
            {
                Range = provisionalTextEdit.Range,
                NewText = string.Empty
            };
        }

        return revertedProvisionalTextEdit;
    }

    private void UpdateVirtualDocument(
        VisualStudioTextChange textChange,
        RazorLanguageKind virtualDocumentKind,
        int hostDocumentVersion,
        Uri documentSnapshotUri,
        Uri virtualDocumentUri)
    {
        if (_documentManager is not TrackingLSPDocumentManager trackingDocumentManager)
        {
            return;
        }

        if (virtualDocumentKind == RazorLanguageKind.CSharp)
        {
            trackingDocumentManager.UpdateVirtualDocument<CSharpVirtualDocument>(
                documentSnapshotUri,
                virtualDocumentUri,
                new[] { textChange },
                hostDocumentVersion,
                state: null);
        }
        else if (virtualDocumentKind == RazorLanguageKind.Html)
        {
            trackingDocumentManager.UpdateVirtualDocument<HtmlVirtualDocument>(
                documentSnapshotUri,
                virtualDocumentUri,
                new[] { textChange },
                hostDocumentVersion,
                state: null);
        }
    }

    [JsonRpcMethod(LanguageServerConstants.RazorCompletionResolveEndpointName, UseSingleObjectParameterDeserialization = true)]
    public async Task<JToken?> ProvideResolvedCompletionItemAsync(DelegatedCompletionItemResolveParams request, CancellationToken cancellationToken)
    {
        string languageServerName;
        bool synchronized;
        VirtualDocumentSnapshot virtualDocumentSnapshot;
        if (request.OriginatingKind == RazorLanguageKind.Html)
        {
            (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
        }
        else if (request.OriginatingKind == RazorLanguageKind.CSharp)
        {
            // TODO this is a partial workaround to fix prefix completion by avoiding sync (which times out during resolve endpoint) if we are currently at a higher version value
            // this does not fix postfix completion and should be superseded by eventual synchronization fix

            var futureDataSyncResult = TryReturnPossiblyFutureSnapshot<CSharpVirtualDocumentSnapshot>(request.Identifier.Version, request.Identifier.TextDocumentIdentifier);
            if (futureDataSyncResult?.Synchronized == true)
            {
                (synchronized, virtualDocumentSnapshot) = futureDataSyncResult;
            }
            else
            {
                (synchronized, virtualDocumentSnapshot) = await TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                    request.Identifier.Version,
                    request.Identifier.TextDocumentIdentifier,
                    cancellationToken);
            }

            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
        }
        else
        {
            Debug.Fail("Unexpected RazorLanguageKind. This can't really happen in a real scenario.");
            return null;
        }

        if (!synchronized)
        {
            // Document was not synchronized
            return null;
        }

        var completionResolveParams = request.CompletionItem;

        var textBuffer = virtualDocumentSnapshot.Snapshot.TextBuffer;
        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalCompletionItem, JToken?>(
            textBuffer,
            Methods.TextDocumentCompletionResolve.Name,
            languageServerName,
            completionResolveParams,
            cancellationToken).ConfigureAwait(false);
        return response?.Response;
    }

    [JsonRpcMethod(LanguageServerConstants.RazorGetFormattingOptionsEndpointName, UseSingleObjectParameterDeserialization = true)]
    public Task<FormattingOptions?> GetFormattingOptionsAsync(TextDocumentIdentifierAndVersion document, CancellationToken _)
    {
        var formattingOptions = _formattingOptionsProvider.GetOptions(document.TextDocumentIdentifier.Uri);
        return Task.FromResult(formattingOptions);
    }

    [JsonRpcMethod(LanguageServerConstants.RazorSnippetCompletionEndpointName, UseSingleObjectParameterDeserialization = true)]
    public CompletionItem[]? GetSnippetCompletions(RazorSnippetCompletionParams completionParams, CancellationToken _)
    {
        if (completionParams.TextSpan.Length == 0)
        {
            return null;
        }

        var snippets = _snippetCache.GetSnippets();

        if (snippets.IsDefaultOrEmpty)
        {
            return null;
        }

        var langSpecificCompletionsEnum = completionParams.LanguageKind switch
        {
            RazorLanguageKind.Html => snippets.Where(static s => s.Language == SnippetLanguage.Html),
            RazorLanguageKind.CSharp => snippets.Where(static s => s.Language == SnippetLanguage.CSharp),
            _ => null
        };

        if (langSpecificCompletionsEnum is null)
        {
            return null;
        }

        if (!_documentManager.TryGetDocument(completionParams.Identifier.Uri, out var snapshot))
        {
            return null;
        }

        var text = snapshot.Snapshot.GetText(completionParams.TextSpan.Start, completionParams.TextSpan.Length);

        var completions = langSpecificCompletionsEnum
            .Where(s => s.Shortcut.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .Select(s => (s, _xmlSnippetParser.GetParsedXmlSnippet(s)))
            .Select(ConvertToCompletionItem)
            .WithoutNull();

        return completions.ToArray();

        CompletionItem? ConvertToCompletionItem((SnippetInfo info, ParsedXmlSnippet? parsed) pair)
        {
            var (info, parsed) = pair;

            if (info is null || parsed is null)
            {
                return null;
            }

            return new()
            {
                Label = info.Shortcut,
                Detail = info.Description,
                InsertTextFormat = InsertTextFormat.Snippet,
                InsertText = GetFormattedLspSnippet(info, parsed, completionParams.TextSpan)
            };
        }
    }

    /// <summary>
    /// Formats the snippet by applying the snippet to the document with the default values / function results for snippet declarations.
    /// Then converts back into an LSP snippet by replacing the declarations with the appropriate LSP tab stops.
    /// 
    /// Note that the operations in this method are sensitive to the context in the document and so must be calculated on each request.
    /// </summary>
    private static string GetFormattedLspSnippet(
        SnippetInfo snippetInfo,
        ParsedXmlSnippet parsedSnippet,
        RazorTextSpan snippetShortcut)
    {
        // Calculate the snippet text with defaults + snippet function results.
        var (snippetFullText, fields, caretSpan) = GetReplacedSnippetText(snippetInfo, snippetShortcut, parsedSnippet);

        // TODO: use the fields? I believe those end up being tab stops
        return snippetFullText.ToString();
    }

    /// <summary>
    /// Create the snippet with the full default text and functions applied.  Output the spans associated with
    /// each field and the final caret location in that text so that we can find those locations later.
    /// </summary>
    private static (string ReplacedSnippetText, ImmutableDictionary<SnippetFieldPart, ImmutableArray<TextSpan>> Fields, TextSpan? CaretSpan) GetReplacedSnippetText(
        SnippetInfo snippetInfo,
        RazorTextSpan snippetSpan,
        ParsedXmlSnippet parsedSnippet)
    {
        // Iterate the snippet parts so that we can do two things:
        //   1.  Calculate the snippet function result.  This must be done against the document containing the default snippet text
        //       as the function result is context dependent.
        //   2.  After inserting the function result, determine the spans associated with each editable snippet field.
        var fieldOffsets = new Dictionary<SnippetFieldPart, ImmutableArray<TextSpan>>();
        using var _ = StringBuilderPool.GetPooledObject(out var functionSnippetBuilder);
        TextSpan? caretSpan = null;

        // This represents the field start location in the context of the snippet without functions replaced (see below).
        var locationInDefaultSnippet = snippetSpan.Start;

        // This represents the field start location in the context of the snippet with functions replaced.
        var locationInFinalSnippet = snippetSpan.Start;
        foreach (var originalPart in parsedSnippet.Parts)
        {
            var part = originalPart;

            // Only store spans for editable fields or the cursor location, we don't need to get back to anything else.
            if (part is SnippetFieldPart fieldPart && fieldPart.EditIndex != null)
            {
                var fieldSpan = new TextSpan(locationInFinalSnippet, part.DefaultText.Length);
                fieldOffsets[fieldPart] = fieldOffsets.GetValueOrDefault(fieldPart, ImmutableArray<TextSpan>.Empty).Add(fieldSpan);
            }
            else if (part is SnippetCursorPart cursorPart)
            {
                caretSpan = new TextSpan(locationInFinalSnippet, cursorPart.DefaultText.Length);
            }
            else if (part is SnippetShortcutPart shortcutPart)
            {
                part = new SnippetShortcutPart(snippetInfo.Shortcut);
            }

            // Append the new snippet part to the text and track the location of the field in the text w/ functions.
            locationInFinalSnippet += part.DefaultText.Length;
            functionSnippetBuilder.Append(part.DefaultText);

            // Keep track of the original field location in the text w/out functions.
            locationInDefaultSnippet += originalPart.DefaultText.Length;
        }

        return (functionSnippetBuilder.ToString(), fieldOffsets.ToImmutableDictionary(), caretSpan);
    }
}
