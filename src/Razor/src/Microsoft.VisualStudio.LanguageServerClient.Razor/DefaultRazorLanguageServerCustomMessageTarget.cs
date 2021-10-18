// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage.Extensions;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using OmniSharpConfigurationParams = OmniSharp.Extensions.LanguageServer.Protocol.Models.ConfigurationParams;
using SemanticTokensParams = OmniSharp.Extensions.LanguageServer.Protocol.Models.SemanticTokensParams;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(RazorLanguageServerCustomMessageTarget))]
    internal class DefaultRazorLanguageServerCustomMessageTarget : RazorLanguageServerCustomMessageTarget
    {
        private readonly TrackingLSPDocumentManager _documentManager;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly RazorUIContextManager _uIContextManager;
        private readonly IDisposable _razorReadyListener;
        private readonly RazorLSPClientOptionsMonitor _clientOptionsMonitor;
        private readonly LSPDocumentSynchronizer _documentSynchronizer;

        private const string RazorReadyFeature = "Razor-Initialization";

        [ImportingConstructor]
        public DefaultRazorLanguageServerCustomMessageTarget(
            LSPDocumentManager documentManager,
            JoinableTaskContext joinableTaskContext,
            LSPRequestInvoker requestInvoker,
            RazorUIContextManager uIContextManager,
            IRazorAsynchronousOperationListenerProviderAccessor asyncOpListenerProvider,
            RazorLSPClientOptionsMonitor clientOptionsMonitor,
            LSPDocumentSynchronizer documentSynchronizer)
                : this(
                    documentManager,
                    joinableTaskContext,
                    requestInvoker,
                    uIContextManager,
                    asyncOpListenerProvider.GetListener(RazorReadyFeature).BeginAsyncOperation(RazorReadyFeature),
                    clientOptionsMonitor,
                    documentSynchronizer)
        {
        }

        // Testing constructor
        internal DefaultRazorLanguageServerCustomMessageTarget(
            LSPDocumentManager documentManager,
            JoinableTaskContext joinableTaskContext,
            LSPRequestInvoker requestInvoker,
            RazorUIContextManager uIContextManager,
            IDisposable razorReadyListener,
            RazorLSPClientOptionsMonitor clientOptionsMonitor,
            LSPDocumentSynchronizer documentSynchronizer)
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

            if (uIContextManager is null)
            {
                throw new ArgumentNullException(nameof(uIContextManager));
            }

            if (razorReadyListener is null)
            {
                throw new ArgumentNullException(nameof(razorReadyListener));
            }

            if (clientOptionsMonitor is null)
            {
                throw new ArgumentNullException(nameof(clientOptionsMonitor));
            }

            if (documentSynchronizer is null)
            {
                throw new ArgumentNullException(nameof(documentSynchronizer));
            }

            _documentManager = documentManager as TrackingLSPDocumentManager;

            if (_documentManager is null)
            {
                throw new ArgumentException("The LSP document manager should be of type " + typeof(TrackingLSPDocumentManager).FullName, nameof(_documentManager));
            }

            _joinableTaskFactory = joinableTaskContext.Factory;
            _requestInvoker = requestInvoker;
            _uIContextManager = uIContextManager;
            _razorReadyListener = razorReadyListener;
            _clientOptionsMonitor = clientOptionsMonitor;
            _documentSynchronizer = documentSynchronizer;
        }

        // Testing constructor
        internal DefaultRazorLanguageServerCustomMessageTarget(TrackingLSPDocumentManager documentManager)
        {
            _documentManager = documentManager;
        }

        public override async Task UpdateCSharpBufferAsync(UpdateBufferRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            UpdateCSharpBuffer(request);
        }

        // Internal for testing
        internal void UpdateCSharpBuffer(UpdateBufferRequest request)
        {
            if (request == null || request.HostDocumentFilePath == null || request.HostDocumentVersion == null)
            {
                return;
            }

            var hostDocumentUri = new Uri(request.HostDocumentFilePath);
            _documentManager.UpdateVirtualDocument<CSharpVirtualDocument>(
                hostDocumentUri,
                request.Changes?.Select(change => change.ToVisualStudioTextChange()).ToArray(),
                request.HostDocumentVersion.Value);
        }

        public override async Task UpdateHtmlBufferAsync(UpdateBufferRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            UpdateHtmlBuffer(request);
        }

        // Internal for testing
        internal void UpdateHtmlBuffer(UpdateBufferRequest request)
        {
            if (request == null || request.HostDocumentFilePath == null || request.HostDocumentVersion == null)
            {
                return;
            }

            var hostDocumentUri = new Uri(request.HostDocumentFilePath);
            _documentManager.UpdateVirtualDocument<HtmlVirtualDocument>(
                hostDocumentUri,
                request.Changes?.Select(change => change.ToVisualStudioTextChange()).ToArray(),
                request.HostDocumentVersion.Value);
        }

        public override async Task<RazorDocumentRangeFormattingResponse> RazorDocumentFormattingAsync(DocumentFormattingParams request, CancellationToken cancellationToken)
        {
            var response = new RazorDocumentRangeFormattingResponse() { Edits = Array.Empty<TextEdit>() };

            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var hostDocumentUri = request.TextDocument.Uri;
            if (!_documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot))
            {
                return response;
            }

            string languageServerName;
            Uri projectedUri;
            if (documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlDocument))
            {
                languageServerName = RazorLSPConstants.HtmlLanguageServerName;
                projectedUri = htmlDocument.Uri;
            }
            else
            {
                Debug.Fail("Unexpected RazorLanguageKind. This can't really happen in a real scenario.");
                return response;
            }

            var formattingParams = new DocumentFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = projectedUri },
                Options = request.Options
            };

            var textBuffer = htmlDocument.Snapshot.TextBuffer;
            var edits = await _requestInvoker.ReinvokeRequestOnServerAsync<DocumentFormattingParams, TextEdit[]>(
                textBuffer,
                Methods.TextDocumentFormattingName,
                languageServerName,
                formattingParams,
                cancellationToken).ConfigureAwait(false);

            response.Edits = edits?.Response ?? Array.Empty<TextEdit>();

            return response;
        }

        public override async Task<RazorDocumentRangeFormattingResponse> RazorRangeFormattingAsync(RazorDocumentRangeFormattingParams request, CancellationToken cancellationToken)
        {
            var response = new RazorDocumentRangeFormattingResponse() { Edits = Array.Empty<TextEdit>() };

            if (request.Kind == RazorLanguageKind.Razor)
            {
                return response;
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var hostDocumentUri = new Uri(request.HostDocumentFilePath);
            if (!_documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot))
            {
                return response;
            }

            string languageServerName;
            Uri projectedUri;
            if (request.Kind == RazorLanguageKind.CSharp &&
                documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDocument))
            {
                languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
                projectedUri = csharpDocument.Uri;
            }
            else
            {
                Debug.Fail("Unexpected RazorLanguageKind. This can't really happen in a real scenario.");
                return response;
            }

            var formattingParams = new DocumentRangeFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = projectedUri },
                Range = request.ProjectedRange,
                Options = request.Options
            };

            var textBuffer = csharpDocument.Snapshot.TextBuffer;
            var edits = await _requestInvoker.ReinvokeRequestOnServerAsync<DocumentRangeFormattingParams, TextEdit[]>(
                textBuffer,
                Methods.TextDocumentRangeFormattingName,
                languageServerName,
                formattingParams,
                cancellationToken).ConfigureAwait(false);

            response.Edits = edits?.Response ?? Array.Empty<TextEdit>();

            return response;
        }

        public override async Task<IReadOnlyList<VSInternalCodeAction>> ProvideCodeActionsAsync(CodeActionParams codeActionParams, CancellationToken cancellationToken)
        {
            if (codeActionParams is null)
            {
                throw new ArgumentNullException(nameof(codeActionParams));
            }

            if (!_documentManager.TryGetDocument(codeActionParams.TextDocument.Uri, out var documentSnapshot))
            {
                return null;
            }

            if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
            {
                return null;
            }

            codeActionParams.TextDocument.Uri = csharpDoc.Uri;

            var textBuffer = csharpDoc.Snapshot.TextBuffer;
            var requests = _requestInvoker.ReinvokeRequestOnMultipleServersAsync<CodeActionParams, IReadOnlyList<VSInternalCodeAction>>(
                textBuffer,
                Methods.TextDocumentCodeActionName,
                SupportsCSharpCodeActions,
                codeActionParams,
                cancellationToken).ConfigureAwait(false);

            var codeActions = new List<VSInternalCodeAction>();
            await foreach (var response in requests)
            {
                if (response.Response != null)
                {
                    codeActions.AddRange(response.Response);
                }
            }

            return codeActions;
        }

        public override async Task<VSInternalCodeAction> ResolveCodeActionsAsync(RazorResolveCodeActionParams resolveCodeActionParams, CancellationToken cancellationToken)
        {
            if (resolveCodeActionParams is null)
            {
                throw new ArgumentNullException(nameof(resolveCodeActionParams));
            }

            if (!_documentManager.TryGetDocument(resolveCodeActionParams.Uri, out var documentSnapshot))
            {
                // Couldn't resolve the document associated with the code action bail out.
                return null;
            }

            var csharpTextBuffer = LanguageServerKind.CSharp.GetTextBuffer(documentSnapshot);
            var codeAction = resolveCodeActionParams.CodeAction;
            var requests = _requestInvoker.ReinvokeRequestOnMultipleServersAsync<VSInternalCodeAction, VSInternalCodeAction>(
                csharpTextBuffer,
                Methods.CodeActionResolveName,
                SupportsCSharpCodeActions,
                codeAction,
                cancellationToken).ConfigureAwait(false);

            await foreach (var response in requests)
            {
                if (response.Response != null)
                {
                    // Only take the first response from a resolution
                    return response.Response;
                }
            }

            return null;
        }

        public override async Task<ProvideSemanticTokensResponse> ProvideSemanticTokensAsync(
            ProvideSemanticTokensParams semanticTokensParams,
            CancellationToken cancellationToken)
        {
            if (semanticTokensParams is null)
            {
                throw new ArgumentNullException(nameof(semanticTokensParams));
            }

            var csharpDoc = GetCSharpDocumentSnapshsot(semanticTokensParams.TextDocument.Uri.ToUri());
            if (csharpDoc is null)
            {
                return null;
            }

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(
                (int)semanticTokensParams.RequiredHostDocumentVersion, csharpDoc, cancellationToken);

            if (!synchronized)
            {
                // If we're unable to synchronize we won't produce useful results, but we have to indicate
                // it's due to out of sync by providing the old version
                return new ProvideSemanticTokensResponse(
                    resultId: null, tokens: null, isFinalized: false, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion);
            }

            var csharpTextDocument = semanticTokensParams.TextDocument with { Uri = csharpDoc.Uri };
            semanticTokensParams = semanticTokensParams with { TextDocument = csharpTextDocument };

            var newParams = new SemanticTokensParams
            {
                TextDocument = semanticTokensParams.TextDocument,
                PartialResultToken = semanticTokensParams.PartialResultToken,
            };

            var csharpResults = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensParams, VSSemanticTokensResponse>(
                Methods.TextDocumentSemanticTokensFullName,
                RazorLSPConstants.RazorCSharpLanguageServerName,
                newParams,
                cancellationToken).ConfigureAwait(false);

            var result = csharpResults.Result;
            var response = new ProvideSemanticTokensResponse(
                result.ResultId, result.Data, result.IsFinalized, semanticTokensParams.RequiredHostDocumentVersion);

            return response;
        }

        public override async Task<ProvideSemanticTokensEditsResponse> ProvideSemanticTokensEditsAsync(
            ProvideSemanticTokensDeltaParams semanticTokensEditsParams,
            CancellationToken cancellationToken)
        {
            if (semanticTokensEditsParams is null)
            {
                throw new ArgumentNullException(nameof(semanticTokensEditsParams));
            }

            var documentUri = semanticTokensEditsParams.TextDocument.Uri.ToUri();
            var csharpDoc = GetCSharpDocumentSnapshsot(documentUri);
            if (csharpDoc is null)
            {
                return null;
            }

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(
                (int)semanticTokensEditsParams.RequiredHostDocumentVersion, csharpDoc, cancellationToken);
            if (!synchronized)
            {
                // If we're unable to synchronize we won't produce useful results
                return new ProvideSemanticTokensEditsResponse(
                    resultId: null, tokens: null, edits: null, isFinalized: true, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion);
            }

            var newParams = new SemanticTokensDeltaParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = csharpDoc.Uri,
                },
                PreviousResultId = semanticTokensEditsParams.PreviousResultId,
            };

            var csharpResponse = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensDeltaParams, SumType<VSSemanticTokensResponse, VSSemanticTokensDeltaResponse>>(
                Methods.TextDocumentSemanticTokensFullDeltaName,
                RazorLSPConstants.RazorCSharpLanguageServerName,
                newParams,
                cancellationToken).ConfigureAwait(false);
            var csharpResults = csharpResponse.Result;

            // Converting from LSP to O# types
            if (csharpResults.Value is VSSemanticTokensResponse tokens)
            {
                var response = new ProvideSemanticTokensEditsResponse(
                    tokens.ResultId, tokens.Data, edits: null, isFinalized: tokens.IsFinalized, hostDocumentSyncVersion: semanticTokensEditsParams.RequiredHostDocumentVersion);
                return response;
            }
            else if (csharpResults.Value is VSSemanticTokensDeltaResponse edits)
            {
                var results = new RazorSemanticTokensEdit[edits.Edits.Length];
                for (var i = 0; i < edits.Edits.Length; i++)
                {
                    var currentEdit = edits.Edits[i];
                    results[i] = new RazorSemanticTokensEdit(currentEdit.Start, currentEdit.DeleteCount, currentEdit.Data);
                }

                var response = new ProvideSemanticTokensEditsResponse(
                    edits.ResultId, tokens: null, edits: results, isFinalized: edits.IsFinalized, hostDocumentSyncVersion: semanticTokensEditsParams.RequiredHostDocumentVersion);
                return response;
            }
            else
            {
                throw new ArgumentException("Returned tokens should be of type VSSemanticTokensResponse or VSSemanticTokensDeltaResponse.");
            }
        }

        private CSharpVirtualDocumentSnapshot GetCSharpDocumentSnapshsot(Uri uri)
        {
            var normalizedString = uri.GetAbsoluteOrUNCPath();
            var normalizedUri = new Uri(WebUtility.UrlDecode(normalizedString));

            if (!_documentManager.TryGetDocument(normalizedUri, out var documentSnapshot))
            {
                return null;
            }

            if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
            {
                return null;
            }

            return csharpDoc;
        }

        public override async Task RazorServerReadyAsync(CancellationToken cancellationToken)
        {
            // Doing both UIContext and BrokeredService while integrating
            await _uIContextManager.SetUIContextAsync(RazorLSPConstants.RazorActiveUIContextGuid, isActive: true, cancellationToken);
            _razorReadyListener.Dispose();
        }

        private static bool SupportsCSharpCodeActions(JToken token)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            var (providesCodeActions, resolvesCodeActions) = serverCapabilities?.CodeActionProvider?.Match(
                boolValue => (boolValue, false),
                options => (true, options.ResolveProvider)) ?? (false, false);

            return providesCodeActions && resolvesCodeActions;
        }

        // NOTE: This method is a polyfill for VS. We only intend to do it this way until VS formally
        // supports sending workspace configuration requests.
        public override Task<object[]> WorkspaceConfigurationAsync(
            OmniSharpConfigurationParams configParams,
            CancellationToken cancellationToken)
        {
            if (configParams is null)
            {
                throw new ArgumentNullException(nameof(configParams));
            }

            var result = new List<object>();
            foreach (var item in configParams.Items)
            {
                // Right now in VS we only care about editor settings, but we should update this logic later if
                // we want to support Razor and HTML settings as well.
                var setting = item.Section == "vs.editor.razor"
                    ? _clientOptionsMonitor.EditorSettings
                    : new object();
                result.Add(setting);
            }

            return Task.FromResult(result.ToArray());
        }
    }
}
