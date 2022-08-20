// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation;
using Microsoft.AspNetCore.Razor.LanguageServer.Folding;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage.Extensions;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using Microsoft.VisualStudio.LanguageServerClient.Razor.WrapWithTag;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using OmniSharpConfigurationParams = OmniSharp.Extensions.LanguageServer.Protocol.Models.ConfigurationParams;
using SemanticTokensRangeParams = Microsoft.VisualStudio.LanguageServer.Protocol.SemanticTokensRangeParams;
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
        private readonly FormattingOptionsProvider _formattingOptionsProvider;
        private readonly EditorSettingsManager _editorSettingsManager;
        private readonly LSPDocumentSynchronizer _documentSynchronizer;

        private const string RazorReadyFeature = "Razor-Initialization";

        [ImportingConstructor]
        public DefaultRazorLanguageServerCustomMessageTarget(
            LSPDocumentManager documentManager,
            JoinableTaskContext joinableTaskContext,
            LSPRequestInvoker requestInvoker,
            RazorUIContextManager uIContextManager,
            IRazorAsynchronousOperationListenerProviderAccessor asyncOpListenerProvider,
            FormattingOptionsProvider formattingOptionsProvider,
            EditorSettingsManager editorSettingsManager,
            LSPDocumentSynchronizer documentSynchronizer)
                : this(
                    documentManager,
                    joinableTaskContext,
                    requestInvoker,
                    uIContextManager,
                    asyncOpListenerProvider.GetListener(RazorReadyFeature).BeginAsyncOperation(RazorReadyFeature),
                    formattingOptionsProvider,
                    editorSettingsManager,
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
            FormattingOptionsProvider formattingOptionsProvider,
            EditorSettingsManager editorSettingsManager, LSPDocumentSynchronizer documentSynchronizer)
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

            _joinableTaskFactory = joinableTaskContext.Factory;
            _requestInvoker = requestInvoker;
            _uIContextManager = uIContextManager;
            _razorReadyListener = razorReadyListener;
            _formattingOptionsProvider = formattingOptionsProvider;
            _editorSettingsManager = editorSettingsManager;
            _documentSynchronizer = documentSynchronizer;
        }

        // Testing constructor
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal DefaultRazorLanguageServerCustomMessageTarget(TrackingLSPDocumentManager documentManager)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
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
            if (request is null || request.HostDocumentFilePath is null || request.HostDocumentVersion is null)
            {
                return;
            }

            var hostDocumentUri = new Uri(request.HostDocumentFilePath);
            _documentManager.UpdateVirtualDocument<CSharpVirtualDocument>(
                hostDocumentUri,
                request.Changes?.Select(change => change.ToVisualStudioTextChange()).ToArray(),
                request.HostDocumentVersion.Value,
                state: null);
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
            if (request is null || request.HostDocumentFilePath is null || request.HostDocumentVersion is null)
            {
                return;
            }

            var hostDocumentUri = new Uri(request.HostDocumentFilePath);
            _documentManager.UpdateVirtualDocument<HtmlVirtualDocument>(
                hostDocumentUri,
                request.Changes?.Select(change => change.ToVisualStudioTextChange()).ToArray(),
                request.HostDocumentVersion.Value,
                state: null);
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

        public override async Task<RazorDocumentRangeFormattingResponse> HtmlOnTypeFormattingAsync(RazorDocumentOnTypeFormattingParams request, CancellationToken cancellationToken)
        {
            var response = new RazorDocumentRangeFormattingResponse() { Edits = Array.Empty<TextEdit>() };

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

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(
                request.HostDocumentVersion, htmlDocument, cancellationToken);

            if (!synchronized)
            {
                return response;
            }

            var formattingParams = new DocumentOnTypeFormattingParams()
            {
                Character = request.Character,
                Position = request.Position,
                TextDocument = new TextDocumentIdentifier() { Uri = projectedUri },
                Options = request.Options
            };

            var textBuffer = htmlDocument.Snapshot.TextBuffer;
            var edits = await _requestInvoker.ReinvokeRequestOnServerAsync<DocumentOnTypeFormattingParams, TextEdit[]>(
                textBuffer,
                Methods.TextDocumentOnTypeFormattingName,
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

        public override async Task<IReadOnlyList<VSInternalCodeAction>?> ProvideCodeActionsAsync(CodeActionParams codeActionParams, CancellationToken cancellationToken)
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

        public override async Task<VSInternalCodeAction?> ResolveCodeActionsAsync(RazorResolveCodeActionParams resolveCodeActionParams, CancellationToken cancellationToken)
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
            var requests = _requestInvoker.ReinvokeRequestOnMultipleServersAsync<VSInternalCodeAction, VSInternalCodeAction?>(
                csharpTextBuffer,
                Methods.CodeActionResolveName,
                SupportsCSharpCodeActions,
                codeAction,
                cancellationToken).ConfigureAwait(false);

            await foreach (var response in requests)
            {
                if (response.Response is not null)
                {
                    // Only take the first response from a resolution
                    return response.Response;
                }
            }

            return null;
        }

        public override async Task<ProvideSemanticTokensResponse?> ProvideSemanticTokensRangeAsync(
            ProvideSemanticTokensRangeParams semanticTokensParams,
            CancellationToken cancellationToken)
        {
            if (semanticTokensParams is null)
            {
                throw new ArgumentNullException(nameof(semanticTokensParams));
            }

            if (semanticTokensParams.Range is null)
            {
                throw new ArgumentNullException(nameof(semanticTokensParams.Range));
            }

            var csharpDoc = GetCSharpDocumentSnapshsot(semanticTokensParams.TextDocument.Uri);
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
                return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion);
            }

            semanticTokensParams.TextDocument.Uri = csharpDoc.Uri;

            var newParams = new SemanticTokensRangeParams
            {
                TextDocument = semanticTokensParams.TextDocument,
                PartialResultToken = semanticTokensParams.PartialResultToken,
                Range = semanticTokensParams.Range,
            };

            var textBuffer = csharpDoc.Snapshot.TextBuffer;
            var csharpResults = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRangeParams, VSSemanticTokensResponse>(
                textBuffer,
                Methods.TextDocumentSemanticTokensRangeName,
                RazorLSPConstants.RazorCSharpLanguageServerName,
                newParams,
                cancellationToken).ConfigureAwait(false);

            var result = csharpResults?.Response;
            if (result is null)
            {
                // Weren't able to re-invoke C# semantic tokens but we have to indicate it's due to out of sync by providing the old version
                return new ProvideSemanticTokensResponse(tokens: null, hostDocumentSyncVersion: csharpDoc.HostDocumentSyncVersion);
            }

            var response = new ProvideSemanticTokensResponse(result.Data, semanticTokensParams.RequiredHostDocumentVersion);

            return response;
        }

        public override async Task<IReadOnlyList<ColorInformation>> ProvideHtmlDocumentColorAsync(DocumentColorParams documentColorParams, CancellationToken cancellationToken)
        {
            if (documentColorParams is null)
            {
                throw new ArgumentNullException(nameof(documentColorParams));
            }

            var htmlDoc = GetHtmlDocumentSnapshsot(documentColorParams.TextDocument.Uri);
            if (htmlDoc is null)
            {
                return Array.Empty<ColorInformation>();
            }

            documentColorParams.TextDocument.Uri = htmlDoc.Uri;
            var htmlTextBuffer = htmlDoc.Snapshot.TextBuffer;
            var requests = _requestInvoker.ReinvokeRequestOnMultipleServersAsync<DocumentColorParams, ColorInformation[]>(
                htmlTextBuffer,
                Methods.DocumentColorRequest.Name,
                SupportsDocumentColor,
                documentColorParams,
                cancellationToken).ConfigureAwait(false);

            var colorInformation = new List<ColorInformation>();
            await foreach (var response in requests)
            {
                if (response.Response is not null)
                {
                    colorInformation.AddRange(response.Response);
                }
            }

            return colorInformation;
        }

        private CSharpVirtualDocumentSnapshot? GetCSharpDocumentSnapshsot(Uri uri)
        {
            var normalizedString = uri.GetAbsoluteOrUNCPath();
            var normalizedUri = new Uri(normalizedString);

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

        private HtmlVirtualDocumentSnapshot? GetHtmlDocumentSnapshsot(Uri uri)
        {
            var normalizedString = uri.GetAbsoluteOrUNCPath();
            var normalizedUri = new Uri(WebUtility.UrlDecode(normalizedString));

            if (!_documentManager.TryGetDocument(normalizedUri, out var documentSnapshot))
            {
                return null;
            }

            if (!documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlDoc))
            {
                return null;
            }

            return htmlDoc;
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

        private static bool SupportsDocumentColor(JToken token)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            var supportsDocumentColor = serverCapabilities?.DocumentColorProvider?.Match(
                boolValue => boolValue,
                options => options != null) ?? false;

            return supportsDocumentColor;
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
                    ? _editorSettingsManager.Current
                    : new object();
                result.Add(setting);
            }

            return Task.FromResult(result.ToArray());
        }

        public override async Task<VSInternalWrapWithTagResponse> RazorWrapWithTagAsync(VSInternalWrapWithTagParams wrapWithParams, CancellationToken cancellationToken)
        {
            // Same as in LanguageServerConstants, and in Web Tools
            const string HtmlWrapWithTagEndpoint = "textDocument/_vsweb_wrapWithTag";

            var response = new VSInternalWrapWithTagResponse(wrapWithParams.Range, Array.Empty<TextEdit>());

            var hostDocumentUri = wrapWithParams.TextDocument.Uri;
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
                Debug.Fail("Unexpected RazorLanguageKind. This shouldn't happen in a real scenario.");
                return response;
            }

            // We call the Html language server to do the actual work here, now that we have the vitrual document that they know about
            var request = new VSInternalWrapWithTagParams(
                wrapWithParams.Range,
                wrapWithParams.TagName,
                wrapWithParams.Options,
                new TextDocumentIdentifier() { Uri = projectedUri });

            var textBuffer = htmlDocument.Snapshot.TextBuffer;
            var result = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalWrapWithTagParams, VSInternalWrapWithTagResponse>(
                textBuffer,
                HtmlWrapWithTagEndpoint,
                languageServerName,
                request,
                cancellationToken).ConfigureAwait(false);

            if (result?.Response is not null)
            {
                response = result.Response;
            }

            return response;
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

            if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
            {
                return null;
            }

            var csharpRequest = new VSInternalInlineCompletionRequest
            {
                Context = inlineCompletionParams.Context,
                Position = inlineCompletionParams.Position,
                TextDocument = new TextDocumentIdentifier { Uri = csharpDoc.Uri, },
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

        public override async Task<RazorFoldingRangeResponse?> ProvideFoldingRangesAsync(RazorFoldingRangeRequestParam foldingRangeParams, CancellationToken cancellationToken)
        {
            if (foldingRangeParams is null)
            {
                throw new ArgumentNullException(nameof(foldingRangeParams));
            }

            var csharpRanges = new List<FoldingRange>();
            var csharpDocument = GetCSharpDocumentSnapshsot(foldingRangeParams.TextDocument.Uri);
            var csharpTask = Task.CompletedTask;
            if (csharpDocument is not null)
            {
                csharpTask = Task.Run(async () =>
                    {
                        var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(
                        foldingRangeParams.HostDocumentVersion, csharpDocument, cancellationToken);

                        if (synchronized)
                        {
                            var csharpRequestParams = new FoldingRangeParams()
                            {
                                TextDocument = new()
                                {
                                    Uri = csharpDocument.Uri
                                }
                            };

                            var request = await _requestInvoker.ReinvokeRequestOnServerAsync<FoldingRangeParams, IEnumerable<FoldingRange>?>(
                                Methods.TextDocumentFoldingRange.Name,
                                RazorLSPConstants.RazorCSharpLanguageServerName,
                                SupportsFoldingRange,
                                csharpRequestParams,
                                cancellationToken).ConfigureAwait(false);

                            var result = request.Result;
                            if (result is not null)
                            {
                                csharpRanges.AddRange(result);
                            }
                        }
                    }, cancellationToken);

            }

            var htmlDocument = GetHtmlDocumentSnapshsot(foldingRangeParams.TextDocument.Uri);
            var htmlRanges = new List<FoldingRange>();
            var htmlTask = Task.CompletedTask;
            if (htmlDocument is not null)
            {
                htmlTask = Task.Run(async () =>
                    {
                        var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(
                        foldingRangeParams.HostDocumentVersion, htmlDocument, cancellationToken);

                        if (synchronized)
                        {
                            var htmlRequestParams = new FoldingRangeParams()
                            {
                                TextDocument = new()
                                {
                                    Uri = htmlDocument.Uri
                                }
                            };

                            var request = await _requestInvoker.ReinvokeRequestOnServerAsync<FoldingRangeParams, IEnumerable<FoldingRange>?>(
                                Methods.TextDocumentFoldingRange.Name,
                                RazorLSPConstants.HtmlLanguageServerName,
                                SupportsFoldingRange,
                                htmlRequestParams,
                                cancellationToken).ConfigureAwait(false);

                            var result = request.Result;
                            if (result is not null)
                            {
                                htmlRanges.AddRange(result);
                            }
                        }
                    }, cancellationToken);
            }

            var allTasks = Task.WhenAll(htmlTask, csharpTask);

            try
            {
                await allTasks.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Return null if any of the tasks getting folding ranges
                // results in an error
                return null;
            }

            return new(htmlRanges.ToImmutableArray(), csharpRanges.ToImmutableArray());
        }

        private static bool SupportsFoldingRange(JToken token)
        {
            var serverCapabilities = token.ToObject<ServerCapabilities>();

            var supportsFoldingRange = serverCapabilities?.FoldingRangeProvider?.Match(
                boolValue => boolValue,
                options => options is not null) ?? false;

            return supportsFoldingRange;
        }

        public override Task<WorkspaceEdit?> ProvideTextPresentationAsync(RazorTextPresentationParams presentationParams, CancellationToken cancellationToken)
        {
            return ProvidePresentationAsync(presentationParams, presentationParams.TextDocument.Uri, presentationParams.HostDocumentVersion, presentationParams.Kind, VSInternalMethods.TextDocumentTextPresentationName, cancellationToken);
        }

        public override Task<WorkspaceEdit?> ProvideUriPresentationAsync(RazorUriPresentationParams presentationParams, CancellationToken cancellationToken)
        {
            return ProvidePresentationAsync(presentationParams, presentationParams.TextDocument.Uri, presentationParams.HostDocumentVersion, presentationParams.Kind, VSInternalMethods.TextDocumentUriPresentationName, cancellationToken);
        }

        public async Task<WorkspaceEdit?> ProvidePresentationAsync<TParams>(TParams presentationParams, Uri hostDocumentUri, int hostDocumentVersion, RazorLanguageKind kind, string methodName, CancellationToken cancellationToken)
            where TParams : notnull, IPresentationParams
        {
            if (!_documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot))
            {
                return null;
            }

            string languageServerName;
            VirtualDocumentSnapshot document;
            if (kind == RazorLanguageKind.CSharp &&
                documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDocument))
            {
                languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
                presentationParams.TextDocument = new TextDocumentIdentifier
                {
                    Uri = csharpDocument.Uri
                };
                document = csharpDocument;
            }
            else if (kind == RazorLanguageKind.Html &&
                documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlDocument))
            {
                languageServerName = RazorLSPConstants.HtmlLanguageServerName;
                presentationParams.TextDocument = new TextDocumentIdentifier
                {
                    Uri = htmlDocument.Uri
                };
                document = htmlDocument;
            }
            else
            {
                Debug.Fail("Unexpected RazorLanguageKind. This can't really happen in a real scenario.");
                return null;
            }

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(hostDocumentVersion, document, cancellationToken);
            if (!synchronized)
            {
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
            var hostDocumentUri = request.HostDocument.Uri;
            if (!_documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot))
            {
                return null;
            }

            string languageServerName;
            Uri projectedUri;
            VirtualDocumentSnapshot virtualDocumentSnapshot;
            if (request.ProjectedKind == RazorLanguageKind.Html &&
                documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlVirtualDocument))
            {
                languageServerName = RazorLSPConstants.HtmlLanguageServerName;
                projectedUri = htmlVirtualDocument.Uri;
                virtualDocumentSnapshot = htmlVirtualDocument;
            }
            else if (request.ProjectedKind == RazorLanguageKind.CSharp &&
                documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpVirtualDocument))
            {
                languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
                projectedUri = csharpVirtualDocument.Uri;
                virtualDocumentSnapshot = csharpVirtualDocument;
            }
            else
            {
                Debug.Fail("Unexpected RazorLanguageKind. This shouldn't really happen in a real scenario.");
                return null;
            }

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(
                request.HostDocument.Version, virtualDocumentSnapshot, rejectOnNewerParallelRequest: false, cancellationToken);

            if (!synchronized)
            {
                return null;
            }

            var completionParams = new CompletionParams()
            {
                Context = request.Context,
                Position = request.ProjectedPosition,
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = projectedUri,
                },
            };

            var continueOnCapturedContext = false;
            var provisionalTextEdit = request.ProvisionalTextEdit;
            if (provisionalTextEdit is not null)
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var provisionalChange = new VisualStudioTextChange(request.ProvisionalTextEdit, virtualDocumentSnapshot.Snapshot);
                UpdateVirtualDocument(provisionalChange, request.ProjectedKind, request.HostDocument.Version, documentSnapshot.Uri);

                // We want the delegation to continue on the captured context because we're currently on the `main` thread and we need to get back to the
                // main thread in order to update the virtual buffer with the reverted text edit.
                continueOnCapturedContext = true;
            }

            try
            {
                var textBuffer = virtualDocumentSnapshot.Snapshot.TextBuffer;
                var response = await _requestInvoker.ReinvokeRequestOnServerAsync<CompletionParams, JToken?>(
                    textBuffer,
                    Methods.TextDocumentCompletion.Name,
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
                    UpdateVirtualDocument(revertedProvisionalChange, request.ProjectedKind, request.HostDocument.Version, documentSnapshot.Uri);
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
            Uri documentSnapshotUri)
        {
            if (_documentManager is not TrackingLSPDocumentManager trackingDocumentManager)
            {
                return;
            }

            if (virtualDocumentKind == RazorLanguageKind.CSharp)
            {
                trackingDocumentManager.UpdateVirtualDocument<CSharpVirtualDocument>(
                    documentSnapshotUri,
                    new[] { textChange },
                    hostDocumentVersion,
                    state: null);
            }
            else if (virtualDocumentKind == RazorLanguageKind.Html)
            {
                trackingDocumentManager.UpdateVirtualDocument<HtmlVirtualDocument>(
                    documentSnapshotUri,
                    new[] { textChange },
                    hostDocumentVersion,
                    state: null);
            }
        }

        public override async Task<JToken?> ProvideResolvedCompletionItemAsync(DelegatedCompletionItemResolveParams request, CancellationToken cancellationToken)
        {
            var hostDocumentUri = request.HostDocument.Uri;
            if (!_documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot))
            {
                return null;
            }

            string languageServerName;
            VirtualDocumentSnapshot virtualDocumentSnapshot;
            if (request.OriginatingKind == RazorLanguageKind.Html &&
                documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlVirtualDocument))
            {
                languageServerName = RazorLSPConstants.HtmlLanguageServerName;
                virtualDocumentSnapshot = htmlVirtualDocument;
            }
            else if (request.OriginatingKind == RazorLanguageKind.CSharp &&
                documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpVirtualDocument))
            {
                languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
                virtualDocumentSnapshot = csharpVirtualDocument;
            }
            else
            {
                Debug.Fail("Unexpected RazorLanguageKind. This can't really happen in a real scenario.");
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

        public override Task<FormattingOptions?> GetFormattingOptionsAsync(TextDocumentIdentifier document, CancellationToken cancellationToken)
        {
            var formattingOptions = _formattingOptionsProvider.GetOptions(document.Uri);
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
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = delegationDetails.Value.ProjectedUri,
                },
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

        public override Task<VSInternalHover?> HoverAsync(DelegatedPositionParams request, CancellationToken cancellationToken)
            => DelegateTextDocumentPositionRequestAsync<VSInternalHover>(request, Methods.TextDocumentHoverName, cancellationToken);

        public override Task<Location[]?> DefinitionAsync(DelegatedPositionParams request, CancellationToken cancellationToken)
            => DelegateTextDocumentPositionRequestAsync<Location[]>(request, Methods.TextDocumentDefinitionName, cancellationToken);

        public override Task<DocumentHighlight[]?> DocumentHighlightAsync(DelegatedPositionParams request, CancellationToken cancellationToken)
            => DelegateTextDocumentPositionRequestAsync<DocumentHighlight[]>(request, Methods.TextDocumentDocumentHighlightName, cancellationToken);

        private async Task<TResult?> DelegateTextDocumentPositionRequestAsync<TResult>(DelegatedPositionParams request, string methodName, CancellationToken cancellationToken)
            where TResult : class
        {
            var delegationDetails = await GetProjectedRequestDetailsAsync(request, cancellationToken).ConfigureAwait(false);
            if (delegationDetails is null)
            {
                return null;
            }

            var positionParams = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = delegationDetails.Value.ProjectedUri,
                },
                Position = request.ProjectedPosition,
            };

            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, TResult?>(
                delegationDetails.Value.TextBuffer,
                methodName,
                delegationDetails.Value.LanguageServerName,
                positionParams,
                cancellationToken).ConfigureAwait(false);

            return response?.Response;
        }

        private async Task<DelegationRequestDetails?> GetProjectedRequestDetailsAsync(IDelegatedParams request, CancellationToken cancellationToken)
        {
            string languageServerName;
            Uri projectedUri;

            if (!_documentManager.TryGetDocument(request.HostDocument.Uri, out var documentSnapshot))
            {
                return null;
            }

            VirtualDocumentSnapshot virtualDocumentSnapshot;
            if (request.ProjectedKind == RazorLanguageKind.Html &&
                documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlVirtualDocument))
            {
                languageServerName = RazorLSPConstants.HtmlLanguageServerName;
                projectedUri = htmlVirtualDocument.Uri;
                virtualDocumentSnapshot = htmlVirtualDocument;
            }
            else if (request.ProjectedKind == RazorLanguageKind.CSharp &&
                documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpVirtualDocument))
            {
                languageServerName = RazorLSPConstants.RazorCSharpLanguageServerName;
                projectedUri = csharpVirtualDocument.Uri;
                virtualDocumentSnapshot = csharpVirtualDocument;
            }
            else
            {
                Debug.Fail("Unexpected RazorLanguageKind. This shouldn't really happen in a real scenario.");
                return null;
            }

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(request.HostDocument.Version, virtualDocumentSnapshot, rejectOnNewerParallelRequest: false, cancellationToken);
            if (!synchronized)
            {
                return null;
            }

            return new DelegationRequestDetails(languageServerName, projectedUri, virtualDocumentSnapshot.Snapshot.TextBuffer);
        }

        private record struct DelegationRequestDetails(string LanguageServerName, Uri ProjectedUri, ITextBuffer TextBuffer);
    }
}
