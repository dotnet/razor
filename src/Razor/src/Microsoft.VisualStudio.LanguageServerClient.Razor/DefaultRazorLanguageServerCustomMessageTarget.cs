﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using OmniSharpConfigurationParams = OmniSharp.Extensions.LanguageServer.Protocol.Models.ConfigurationParams;
using SemanticTokens = OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals.SemanticTokens;
using SemanticTokensParams = OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals.SemanticTokensParams;
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
            LSPDocumentSynchronizer documentSynchronizer) :
                this(
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
            else if (request.Kind == RazorLanguageKind.Html &&
                documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlDocument))
            {
                languageServerName = RazorLSPConstants.HtmlLanguageServerName;
                projectedUri = htmlDocument.Uri;
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

            var edits = await _requestInvoker.ReinvokeRequestOnServerAsync<DocumentRangeFormattingParams, TextEdit[]>(
                Methods.TextDocumentRangeFormattingName,
                languageServerName,
                formattingParams,
                cancellationToken).ConfigureAwait(false);

            response.Edits = edits.Result ?? Array.Empty<TextEdit>();

            return response;
        }

        public override async Task<VSCodeAction[]> ProvideCodeActionsAsync(CodeActionParams codeActionParams, CancellationToken cancellationToken)
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

            var results = await _requestInvoker.ReinvokeRequestOnMultipleServersAsync<CodeActionParams, VSCodeAction[]>(
                Methods.TextDocumentCodeActionName,
                LanguageServerKind.CSharp.ToContentType(),
                SupportsCSharpCodeActions,
                codeActionParams,
                cancellationToken).ConfigureAwait(false);

            return results.SelectMany(l => l.Result).ToArray();
        }

        public override async Task<VSCodeAction> ResolveCodeActionsAsync(VSCodeAction codeAction, CancellationToken cancellationToken)
        {
            if (codeAction is null)
            {
                throw new ArgumentNullException(nameof(codeAction));
            }

            var results = await _requestInvoker.ReinvokeRequestOnMultipleServersAsync<VSCodeAction, VSCodeAction>(
                MSLSPMethods.TextDocumentCodeActionResolveName,
                LanguageServerKind.CSharp.ToContentType(),
                SupportsCSharpCodeActions,
                codeAction,
                cancellationToken).ConfigureAwait(false);

            return results.FirstOrDefault(c => c.Result != null).Result;
        }

        [Obsolete]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
        public override async Task<ProvideSemanticTokensResponse> ProvideSemanticTokensAsync(
            ProvideSemanticTokensParams semanticTokensParams,
            CancellationToken cancellationToken)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
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

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync((int)semanticTokensParams.RequiredHostDocumentVersion, csharpDoc, cancellationToken);

            if (!synchronized)
            {
                // If we're unable to synchronize we won't produce useful results, but we have to indicate it's due to out of sync by providing the old version
                return new ProvideSemanticTokensResponse(result: null, csharpDoc.HostDocumentSyncVersion);
            }

            semanticTokensParams.TextDocument.Uri = csharpDoc.Uri;

            var newParams = new SemanticTokensParams
            {
                TextDocument = semanticTokensParams.TextDocument,
                PartialResultToken = semanticTokensParams.PartialResultToken,
            };
            var csharpResults = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensParams, SemanticTokens>(
                LanguageServerConstants.LegacyRazorSemanticTokensEndpoint,
                RazorLSPConstants.RazorCSharpLanguageServerName,
                newParams,
                cancellationToken).ConfigureAwait(false);

            var result = new ProvideSemanticTokensResponse(csharpResults.Result, semanticTokensParams.RequiredHostDocumentVersion);

            return result;
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

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync((int)semanticTokensEditsParams.RequiredHostDocumentVersion, csharpDoc, cancellationToken);

            if (!synchronized)
            {
                // If we're unable to synchronize we won't produce useful results 
                return new ProvideSemanticTokensEditsResponse(tokens: null, edits: null, resultId: null, csharpDoc.HostDocumentSyncVersion);
            }

            var newParams = new SemanticTokensEditsParams
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = csharpDoc.Uri,
                },
                PreviousResultId = semanticTokensEditsParams.PreviousResultId,
            };

            var csharpResponse = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensEditsParams, SumType<LanguageServer.Protocol.SemanticTokens, SemanticTokensEdits>>(
                LanguageServerConstants.LegacyRazorSemanticTokensEditEndpoint,
                RazorLSPConstants.RazorCSharpLanguageServerName,
                newParams,
                cancellationToken).ConfigureAwait(false);
            var csharpResults = csharpResponse.Result;

            // Converting from LSP to O# types
            if (csharpResults.Value is LanguageServer.Protocol.SemanticTokens tokens)
            {
                var response = new ProvideSemanticTokensEditsResponse(tokens.Data, edits: null, tokens.ResultId, semanticTokensEditsParams.RequiredHostDocumentVersion);
                return response;
            }
            else if (csharpResults.Value is SemanticTokensEdits edits)
            {
                var results = new RazorSemanticTokensEdit[edits.Edits.Length];
                for (var i = 0; i < edits.Edits.Length; i++)
                {
                    var currentEdit = edits.Edits[i];
                    results[i] = new RazorSemanticTokensEdit(currentEdit.Start, currentEdit.DeleteCount, currentEdit.Data);
                }

                var response = new ProvideSemanticTokensEditsResponse(tokens: null, results, edits.ResultId, semanticTokensEditsParams.RequiredHostDocumentVersion);
                return response;
            }
            else
            {
                throw new ArgumentException("Returned tokens should be of type SemanticTokens or SemanticTokensEdits.");
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
            var serverCapabilities = token.ToObject<VSServerCapabilities>();

            var providesCodeActions = serverCapabilities?.CodeActionProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;

            var resolvesCodeActions = serverCapabilities?.CodeActionsResolveProvider == true;

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
