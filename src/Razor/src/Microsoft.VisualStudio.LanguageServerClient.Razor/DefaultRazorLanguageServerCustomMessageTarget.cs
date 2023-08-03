// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.ColorPresentation;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentColor;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Folding;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Editor.Razor.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.WrapWithTag;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using ImplementationResult = Microsoft.VisualStudio.LanguageServer.Protocol.SumType<
    Microsoft.VisualStudio.LanguageServer.Protocol.Location[],
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalReferenceItem[]>;
using SemanticTokensRangeParams = Microsoft.VisualStudio.LanguageServer.Protocol.SemanticTokensRangeParams;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

[Export(typeof(RazorLanguageServerCustomMessageTarget))]
internal partial class DefaultRazorLanguageServerCustomMessageTarget : RazorLanguageServerCustomMessageTarget
{
    private readonly TrackingLSPDocumentManager _documentManager;
    private readonly JoinableTaskFactory _joinableTaskFactory;
    private readonly LSPRequestInvoker _requestInvoker;
    private readonly ITelemetryReporter _telemetryReporter;
    private readonly FormattingOptionsProvider _formattingOptionsProvider;
    private readonly IClientSettingsManager _editorSettingsManager;
    private readonly LSPDocumentSynchronizer _documentSynchronizer;
    private readonly IOutputWindowLogger? _outputWindowLogger;

    [ImportingConstructor]
    public DefaultRazorLanguageServerCustomMessageTarget(
        LSPDocumentManager documentManager,
        JoinableTaskContext joinableTaskContext,
        LSPRequestInvoker requestInvoker,
        FormattingOptionsProvider formattingOptionsProvider,
        IClientSettingsManager editorSettingsManager,
        LSPDocumentSynchronizer documentSynchronizer,
        ITelemetryReporter telemetryReporter,
        [Import(AllowDefault = true)] IOutputWindowLogger? outputWindowLogger)
    {
        if (documentManager is null)
        {
            throw new ArgumentNullException(nameof(documentManager));
        }

        if (joinableTaskContext is null)
        {
            throw new ArgumentNullException(nameof(joinableTaskContext));
        }

        if (requestInvoker is null)
        {
            throw new ArgumentNullException(nameof(requestInvoker));
        }

        if (formattingOptionsProvider is null)
        {
            throw new ArgumentNullException(nameof(formattingOptionsProvider));
        }

        if (editorSettingsManager is null)
        {
            throw new ArgumentNullException(nameof(editorSettingsManager));
        }

        if (documentSynchronizer is null)
        {
            throw new ArgumentNullException(nameof(documentSynchronizer));
        }

        _documentManager = (TrackingLSPDocumentManager)documentManager;

        if (_documentManager is null)
        {
            throw new ArgumentException("The LSP document manager should be of type " + typeof(TrackingLSPDocumentManager).FullName, nameof(_documentManager));
        }

        if (telemetryReporter is null)
        {
            throw new ArgumentNullException(nameof(telemetryReporter));
        }

        _joinableTaskFactory = joinableTaskContext.Factory;

        _requestInvoker = requestInvoker;
        _formattingOptionsProvider = formattingOptionsProvider;
        _editorSettingsManager = editorSettingsManager;
        _documentSynchronizer = documentSynchronizer;
        _telemetryReporter = telemetryReporter;
        _outputWindowLogger = outputWindowLogger;
    }

    // Testing constructor
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    internal DefaultRazorLanguageServerCustomMessageTarget(TrackingLSPDocumentManager documentManager,
        LSPDocumentSynchronizer documentSynchronizer)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        _documentManager = documentManager;
        _documentSynchronizer = documentSynchronizer;
    }

    public override async Task<VSInternalInlineCompletionList?> ProvideInlineCompletionAsync(RazorInlineCompletionRequest inlineCompletionParams, CancellationToken cancellationToken)
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

    public override Task<WorkspaceEdit?> ProvideTextPresentationAsync(RazorTextPresentationParams presentationParams, CancellationToken cancellationToken)
    {
        return ProvidePresentationAsync(presentationParams, presentationParams.HostDocumentVersion, presentationParams.Kind, VSInternalMethods.TextDocumentTextPresentationName, cancellationToken);
    }

    public override Task<WorkspaceEdit?> ProvideUriPresentationAsync(RazorUriPresentationParams presentationParams, CancellationToken cancellationToken)
    {
        return ProvidePresentationAsync(presentationParams, presentationParams.HostDocumentVersion, presentationParams.Kind, VSInternalMethods.TextDocumentUriPresentationName, cancellationToken);
    }

    public async Task<WorkspaceEdit?> ProvidePresentationAsync<TParams>(TParams presentationParams, int hostDocumentVersion, RazorLanguageKind kind, string methodName, CancellationToken cancellationToken)
        where TParams : notnull, IPresentationParams
    {
        string languageServerName;
        VirtualDocumentSnapshot document;
        if (kind == RazorLanguageKind.CSharp)
        {
            var syncResult = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                _documentManager,
                hostDocumentVersion,
                presentationParams.TextDocument,
                cancellationToken);
            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
            presentationParams.TextDocument = new TextDocumentIdentifier
            {
                Uri = syncResult.VirtualSnapshot.Uri,
            };
            document = syncResult.VirtualSnapshot;
        }
        else if (kind == RazorLanguageKind.Html)
        {
            var syncResult = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                _documentManager,
                hostDocumentVersion,
                presentationParams.TextDocument,
                cancellationToken);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
            presentationParams.TextDocument = new TextDocumentIdentifier
            {
                Uri = syncResult.VirtualSnapshot.Uri,
            };
            document = syncResult.VirtualSnapshot;
        }
        else
        {
            Debug.Fail("Unexpected RazorLanguageKind. This can't really happen in a real scenario.");
            return null;
        }

        var textBuffer = document.Snapshot.TextBuffer;
        var result = await _requestInvoker.ReinvokeRequestOnServerAsync<TParams, WorkspaceEdit?>(
            textBuffer,
            methodName,
            languageServerName,
            presentationParams,
            cancellationToken).ConfigureAwait(false);

        return result?.Response;
    }

    // JToken returning because there's no value in converting the type into its final type because this method serves entirely as a delegation point (immedaitely re-serializes).
    public override async Task<JToken?> ProvideCompletionsAsync(
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
            (synchronized, virtualDocumentSnapshot) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                _documentManager,
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
            projectedUri = virtualDocumentSnapshot.Uri;
        }
        else if (request.ProjectedKind == RazorLanguageKind.CSharp)
        {
            (synchronized, virtualDocumentSnapshot) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                _documentManager,
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

    public override async Task<JToken?> ProvideResolvedCompletionItemAsync(DelegatedCompletionItemResolveParams request, CancellationToken cancellationToken)
    {
        string languageServerName;
        bool synchronized;
        VirtualDocumentSnapshot virtualDocumentSnapshot;
        if (request.OriginatingKind == RazorLanguageKind.Html)
        {
            (synchronized, virtualDocumentSnapshot) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                _documentManager,
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
        }
        else if (request.OriginatingKind == RazorLanguageKind.CSharp)
        {
            // TODO this is a partial workaround to fix prefix completion by avoiding sync (which times out during resolve endpoint) if we are currently at a higher version value
            // this does not fix postfix completion and should be superseded by eventual synchronization fix

            var futureDataSyncResult =
                (_documentSynchronizer as DefaultLSPDocumentSynchronizer)?.TryReturnPossiblyFutureSnapshot<CSharpVirtualDocumentSnapshot>(
                    _documentManager,
                    request.Identifier.Version,
                    request.Identifier.TextDocumentIdentifier);
            if (futureDataSyncResult?.Synchronized == true)
            {
                (synchronized, virtualDocumentSnapshot) = futureDataSyncResult;
            }
            else
            {
                (synchronized, virtualDocumentSnapshot) = await _documentSynchronizer
                        .TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                            _documentManager,
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

    public override Task<FormattingOptions?> GetFormattingOptionsAsync(TextDocumentIdentifierAndVersion document, CancellationToken cancellationToken)
    {
        var formattingOptions = _formattingOptionsProvider.GetOptions(document.TextDocumentIdentifier.Uri);
        return Task.FromResult(formattingOptions);
    }

    public override async Task<WorkspaceEdit?> RenameAsync(DelegatedRenameParams request, CancellationToken cancellationToken)
    {
        var delegationDetails = await GetProjectedRequestDetailsAsync(request, cancellationToken).ConfigureAwait(false);
        if (delegationDetails is null)
        {
            return null;
        }

        var renameParams = new RenameParams()
        {
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(delegationDetails.Value.ProjectedUri),
            Position = request.ProjectedPosition,
            NewName = request.NewName,
        };

        var textBuffer = delegationDetails.Value.TextBuffer;
        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<RenameParams, WorkspaceEdit?>(
            textBuffer,
            Methods.TextDocumentRenameName,
            delegationDetails.Value.LanguageServerName,
            renameParams,
            cancellationToken).ConfigureAwait(false);
        return response?.Response;
    }

    public override async Task<VSInternalDocumentOnAutoInsertResponseItem?> OnAutoInsertAsync(DelegatedOnAutoInsertParams request, CancellationToken cancellationToken)
    {
        var delegationDetails = await GetProjectedRequestDetailsAsync(request, cancellationToken).ConfigureAwait(false);
        if (delegationDetails is null)
        {
            return default;
        }

        var onAutoInsertParams = new VSInternalDocumentOnAutoInsertParams
        {
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(delegationDetails.Value.ProjectedUri),
            Position = request.ProjectedPosition,
            Character = request.Character,
            Options = request.Options
        };

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem?>(
           delegationDetails.Value.TextBuffer,
           VSInternalMethods.OnAutoInsertName,
           delegationDetails.Value.LanguageServerName,
           onAutoInsertParams,
           cancellationToken).ConfigureAwait(false);
        return response?.Response;
    }

    public override async Task<Range?> ValidateBreakpointRangeAsync(DelegatedValidateBreakpointRangeParams request, CancellationToken cancellationToken)
    {
        var delegationDetails = await GetProjectedRequestDetailsAsync(request, cancellationToken).ConfigureAwait(false);
        if (delegationDetails is null)
        {
            return default;
        }

        var validateBreakpointRangeParams = new VSInternalValidateBreakableRangeParams
        {
            TextDocument = request.Identifier.TextDocumentIdentifier.WithUri(delegationDetails.Value.ProjectedUri),
            Range = request.ProjectedRange
        };

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalValidateBreakableRangeParams, Range?>(
            delegationDetails.Value.TextBuffer,
            VSInternalMethods.TextDocumentValidateBreakableRangeName,
            delegationDetails.Value.LanguageServerName,
            validateBreakpointRangeParams,
            cancellationToken).ConfigureAwait(false);
        return response?.Response;
    }

    public override Task<VSInternalHover?> HoverAsync(DelegatedPositionParams request, CancellationToken cancellationToken)
        => DelegateTextDocumentPositionAndProjectContextAsync<VSInternalHover>(request, Methods.TextDocumentHoverName, cancellationToken);

    public override Task<Location[]?> DefinitionAsync(DelegatedPositionParams request, CancellationToken cancellationToken)
        => DelegateTextDocumentPositionAndProjectContextAsync<Location[]>(request, Methods.TextDocumentDefinitionName, cancellationToken);

    public override Task<DocumentHighlight[]?> DocumentHighlightAsync(DelegatedPositionParams request, CancellationToken cancellationToken)
        => DelegateTextDocumentPositionAndProjectContextAsync<DocumentHighlight[]>(request, Methods.TextDocumentDocumentHighlightName, cancellationToken);

    public override Task<SignatureHelp?> SignatureHelpAsync(DelegatedPositionParams request, CancellationToken cancellationToken)
        => DelegateTextDocumentPositionAndProjectContextAsync<SignatureHelp>(request, Methods.TextDocumentSignatureHelpName, cancellationToken);

    public override Task<ImplementationResult> ImplementationAsync(DelegatedPositionParams request, CancellationToken cancellationToken)
        => DelegateTextDocumentPositionAndProjectContextAsync<ImplementationResult>(request, Methods.TextDocumentImplementationName, cancellationToken);

    public override Task<VSInternalReferenceItem[]?> ReferencesAsync(DelegatedPositionParams request, CancellationToken cancellationToken)
        => DelegateTextDocumentPositionAndProjectContextAsync<VSInternalReferenceItem[]>(request, Methods.TextDocumentReferencesName, cancellationToken);

    public override async Task<RazorPullDiagnosticResponse?> DiagnosticsAsync(DelegatedDiagnosticParams request, CancellationToken cancellationToken)
    {
        var csharpTask = Task.Run(() => GetVirtualDocumentPullDiagnosticsAsync<CSharpVirtualDocumentSnapshot>(request.Identifier, request.CorrelationId, RazorLSPConstants.RazorCSharpLanguageServerName, cancellationToken), cancellationToken);
        var htmlTask = Task.Run(() => GetVirtualDocumentPullDiagnosticsAsync<HtmlVirtualDocumentSnapshot>(request.Identifier, request.CorrelationId, RazorLSPConstants.HtmlLanguageServerName, cancellationToken), cancellationToken);

        try
        {
            await Task.WhenAll(htmlTask, csharpTask).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                _outputWindowLogger?.LogError(e, "Exception thrown in PullDiagnostic delegation");
            }
            // Return null if any of the tasks getting diagnostics results in an error
            return null;
        }

        var csharpDiagnostics = await csharpTask.ConfigureAwait(false);
        var htmlDiagnostics = await htmlTask.ConfigureAwait(false);

        if (csharpDiagnostics is null || htmlDiagnostics is null)
        {
            // If either is null we don't have a complete view and returning anything will cause us to "lock-in" incomplete info. So we return null and wait for a re-try.
            return null;
        }

        return new RazorPullDiagnosticResponse(csharpDiagnostics, htmlDiagnostics);
    }

    private async Task<VSInternalDiagnosticReport[]?> GetVirtualDocumentPullDiagnosticsAsync<TVirtualDocumentSnapshot>(TextDocumentIdentifierAndVersion identifier, Guid correlationId, string delegatedLanguageServerName, CancellationToken cancellationToken)
        where TVirtualDocumentSnapshot : VirtualDocumentSnapshot
    {
        var (synchronized, virtualDocument) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<TVirtualDocumentSnapshot>(
            _documentManager,
            identifier.Version,
            identifier.TextDocumentIdentifier,
            cancellationToken).ConfigureAwait(false);
        if (!synchronized)
        {
            return null;
        }

        var request = new VSInternalDocumentDiagnosticsParams
        {
            TextDocument = identifier.TextDocumentIdentifier.WithUri(virtualDocument.Uri),
        };

        var lspMethodName = VSInternalMethods.DocumentPullDiagnosticName;
        using var _ = _telemetryReporter.TrackLspRequest(lspMethodName, delegatedLanguageServerName, correlationId);
        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]?>(
            virtualDocument.Snapshot.TextBuffer,
            lspMethodName,
            delegatedLanguageServerName,
            request,
            cancellationToken).ConfigureAwait(false);

        // If the delegated server wants to remove all diagnostics about a document, they will send back a response with an item, but that
        // item will have null diagnostics (and every other property). We don't want to propagate that back out to the client, because
        // it would make the client remove all diagnostics for the .razor file, including potentially any returned from other delegated
        // servers.
        if (response?.Response is null or [{ Diagnostics: null }, ..])
        {
            // Important that we send back an empty list here, because null would result it the above method throwing away any other
            // diagnostics it receives from the other delegated server
            return Array.Empty<VSInternalDiagnosticReport>();
        }

        return response.Response;
    }

    public override async Task<VSInternalSpellCheckableRangeReport[]> SpellCheckAsync(DelegatedSpellCheckParams request, CancellationToken cancellationToken)
    {
        var hostDocument = request.Identifier.TextDocumentIdentifier;
        var (synchronized, virtualDocument) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
            _documentManager,
            request.Identifier.Version,
            hostDocument,
            cancellationToken).ConfigureAwait(false);
        if (!synchronized)
        {
            return Array.Empty<VSInternalSpellCheckableRangeReport>();
        }

        var spellCheckParams = new VSInternalDocumentSpellCheckableParams
        {
            TextDocument = hostDocument.WithUri(virtualDocument.Uri),
        };

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalDocumentSpellCheckableParams, VSInternalSpellCheckableRangeReport[]>(
            virtualDocument.Snapshot.TextBuffer,
            VSInternalMethods.TextDocumentSpellCheckableRangesName,
            RazorLSPConstants.RazorCSharpLanguageServerName,
            SupportsSpellCheck,
            spellCheckParams,
            cancellationToken).ConfigureAwait(false);

        return response?.Response ?? Array.Empty<VSInternalSpellCheckableRangeReport>();
    }

    private static bool SupportsSpellCheck(JToken token)
    {
        var serverCapabilities = token.ToObject<VSInternalServerCapabilities>();

        return serverCapabilities?.SpellCheckingProvider ?? false;
    }

    public override async Task<VSProjectContextList?> ProjectContextsAsync(DelegatedProjectContextsParams request, CancellationToken cancellationToken)
    {
        var hostDocument = request.Identifier.TextDocumentIdentifier;
        var (synchronized, virtualDocument) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
            _documentManager,
            request.Identifier.Version,
            hostDocument,
            cancellationToken).ConfigureAwait(false);

        if (!synchronized)
        {
            return null;
        }

        var projectContextParams = new VSGetProjectContextsParams()
        {
            TextDocument = new TextDocumentItem()
            {
                LanguageId = CodeAnalysis.LanguageNames.CSharp,
                Uri = virtualDocument.Uri,
                Version = virtualDocument.Snapshot.Version.VersionNumber,
                Text = virtualDocument.Snapshot.GetText(),
            }
        };

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSGetProjectContextsParams, VSProjectContextList?>(
            virtualDocument.Snapshot.TextBuffer,
            VSMethods.GetProjectContextsName,
            RazorLSPConstants.RazorCSharpLanguageServerName,
            projectContextParams,
            cancellationToken).ConfigureAwait(false);

        return response?.Response;
    }

    public override async Task<SumType<DocumentSymbol[], SymbolInformation[]>?> DocumentSymbolsAsync(DelegatedDocumentSymbolParams request, CancellationToken cancellationToken)
    {
        var hostDocument = request.Identifier.TextDocumentIdentifier;
        var (synchronized, virtualDocument) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
            _documentManager,
            request.Identifier.Version,
            hostDocument,
            cancellationToken).ConfigureAwait(false);

        if (!synchronized)
        {
            return null;
        }

        var documentSymbolParams = new DocumentSymbolParams()
        {
            TextDocument = hostDocument.WithUri(virtualDocument.Uri)
        };

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<DocumentSymbolParams, SumType<DocumentSymbol[], SymbolInformation[]>?>(
            virtualDocument.Snapshot.TextBuffer,
            Methods.TextDocumentDocumentSymbolName,
            RazorLSPConstants.RazorCSharpLanguageServerName,
            documentSymbolParams,
            cancellationToken).ConfigureAwait(false);

        return response?.Response;
    }

    private async Task<TResult?> DelegateTextDocumentPositionAndProjectContextAsync<TResult>(DelegatedPositionParams request, string methodName, CancellationToken cancellationToken)
    {
        var delegationDetails = await GetProjectedRequestDetailsAsync(request, cancellationToken).ConfigureAwait(false);
        if (delegationDetails is null)
        {
            return default;
        }

        var positionParams = new TextDocumentPositionParams()
        {
            TextDocument = new VSTextDocumentIdentifier()
            {
                Uri = delegationDetails.Value.ProjectedUri,
                ProjectContext = null,
            },
            Position = request.ProjectedPosition,
        };

        var response = await _requestInvoker.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, TResult?>(
            delegationDetails.Value.TextBuffer,
            methodName,
            delegationDetails.Value.LanguageServerName,
            positionParams,
            cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            return default;
        }

        return response.Response;
    }

    private async Task<DelegationRequestDetails?> GetProjectedRequestDetailsAsync(IDelegatedParams request, CancellationToken cancellationToken)
    {
        string languageServerName;

        bool synchronized;
        VirtualDocumentSnapshot virtualDocumentSnapshot;
        if (request.ProjectedKind == RazorLanguageKind.Html)
        {
            (synchronized, virtualDocumentSnapshot) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(
                _documentManager,
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken,
                rejectOnNewerParallelRequest: false);
            languageServerName = RazorLSPConstants.HtmlLanguageServerName;
        }
        else if (request.ProjectedKind == RazorLanguageKind.CSharp)
        {
            (synchronized, virtualDocumentSnapshot) = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                _documentManager,
                request.Identifier.Version,
                request.Identifier.TextDocumentIdentifier,
                cancellationToken,
                rejectOnNewerParallelRequest: false);
            languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
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

        return new DelegationRequestDetails(languageServerName, virtualDocumentSnapshot.Uri, virtualDocumentSnapshot.Snapshot.TextBuffer);
    }

    private record struct DelegationRequestDetails(string LanguageServerName, Uri ProjectedUri, ITextBuffer TextBuffer);
}
