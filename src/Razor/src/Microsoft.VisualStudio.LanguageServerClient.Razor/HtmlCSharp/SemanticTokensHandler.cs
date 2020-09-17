// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;
using SemanticTokens = Microsoft.VisualStudio.LanguageServer.Protocol.SemanticTokens;
using SemanticTokensParams = Microsoft.VisualStudio.LanguageServer.Protocol.SemanticTokensParams;
using SemanticTokensRangeParams = Microsoft.VisualStudio.LanguageServer.Protocol.SemanticTokensRangeParams;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(LanguageServerConstants.RazorSemanticTokensEndpoint)]
    internal class FullDocumentSemanticTokenHandler : IRequestHandler<SemanticTokensParams, SemanticTokens>
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;

        [ImportingConstructor]
        public FullDocumentSemanticTokenHandler(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider)
        {
            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (projectionProvider is null)
            {
                throw new ArgumentNullException(nameof(projectionProvider));
            }

            if (documentMappingProvider is null)
            {
                throw new ArgumentNullException(nameof(documentMappingProvider));
            }

            _requestInvoker = requestInvoker;
            _documentManager = documentManager;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;
        }

        public async Task<SemanticTokens> HandleRequestAsync(SemanticTokensParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (clientCapabilities is null)
            {
                throw new ArgumentNullException(nameof(clientCapabilities));
            }

            if (!_documentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                return null;
            }

            var razorContentType = RazorLanguageKind.Razor.ToContainedLanguageContentType();
            var razorResult = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensParams, SemanticTokens>(
                LanguageServerConstants.LegacyRazorSemanticTokensEndpoint,
                razorContentType,
                request,
                cancellationToken).ConfigureAwait(false);


            if (documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var cSharpDocument))
            {
                request.TextDocument = new TextDocumentIdentifier
                {
                    Uri = cSharpDocument.Uri
                };

                var cSharpContentType = RazorLanguageKind.CSharp.ToContainedLanguageContentType();
                var cSharpResult = await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensParams, SemanticTokens>(
                    LanguageServerConstants.LegacyRazorSemanticTokensEndpoint,
                    cSharpContentType,
                    request,
                    cancellationToken).ConfigureAwait(false);
            }

            throw new NotImplementedException();
        }
    }

    [Shared]
    [ExportLspMethod(LanguageServerConstants.LegacyRazorSemanticTokensEditEndpoint)]
    [Obsolete]
    internal class DeltaSemanticTokenHandler : IRequestHandler<SemanticTokensEditsParams, SemanticTokensFullOrDelta>
    {
        public Task<SemanticTokensFullOrDelta> HandleRequestAsync(SemanticTokensEditsParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    [Shared]
    [ExportLspMethod(LanguageServerConstants.LegacyRazorSemanticTokensRangeEndpoint)]
    internal class RangedSemanticTokenHandler : IRequestHandler<SemanticTokensRangeParams, SemanticTokens>
    {
        public Task<SemanticTokens> HandleRequestAsync(SemanticTokensRangeParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
