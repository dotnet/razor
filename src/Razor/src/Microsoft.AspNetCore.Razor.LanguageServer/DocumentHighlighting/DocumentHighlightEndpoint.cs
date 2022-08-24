// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentHighlighting
{
    internal class DocumentHighlightEndpoint : AbstractRazorDelegatingEndpoint<DocumentHighlightParamsBridge, DocumentHighlight[]>, IDocumentHighlightEndpoint
    {
        private readonly RazorDocumentMappingService _documentMappingService;

        public DocumentHighlightEndpoint(
            DocumentContextFactory documentContextFactory,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
            : base(documentContextFactory, languageServerFeatureOptions, documentMappingService, languageServer, loggerFactory.CreateLogger<DocumentHighlightEndpoint>())
        {
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string ServerCapability = "documentHighlightProvider";
            var options = new DocumentHighlightOptions
            {
                WorkDoneProgress = false
            };

            return new RegistrationExtensionResult(ServerCapability, options);
        }

        /// <inheritdoc/>
        protected override string CustomMessageTarget => RazorLanguageServerCustomMessageTargets.RazorDocumentHighlightEndpointName;

        /// <inheritdoc/>
        protected override Task<DocumentHighlight[]?> TryHandleAsync(DocumentHighlightParamsBridge request, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken)
        {
            // We don't handle this in any particular way for Razor, we just delegate
            return Task.FromResult<DocumentHighlight[]?>(null);
        }

        /// <inheritdoc/>
        protected override IDelegatedParams CreateDelegatedParams(DocumentHighlightParamsBridge request, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken)
            => new DelegatedPositionParams(
                    documentContext.Identifier,
                    projection.Position,
                    projection.LanguageKind);

        /// <inheritdoc/>
        protected override async Task<DocumentHighlight[]> HandleDelegatedResponseAsync(DocumentHighlight[] response, DocumentContext documentContext, CancellationToken cancellationToken)
        {
            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

            foreach (var highlight in response)
            {
                if (_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, highlight.Range, out var mappedRange))
                {
                    highlight.Range = mappedRange;
                }
            }

            return response;
        }
    }
}
