// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    internal class RazorHoverEndpoint : AbstractRazorDelegatingEndpoint<VSHoverParamsBridge, VSInternalHover?, DelegatedHoverParams>, IVSHoverEndpoint
    {
        private readonly DocumentContextFactory _documentContextFactory;
        private readonly RazorHoverInfoService _hoverInfoService;
        private readonly RazorDocumentMappingService _documentMappingService;
        private VSInternalClientCapabilities? _clientCapabilities;

        public RazorHoverEndpoint(
            DocumentContextFactory documentContextFactory,
            RazorHoverInfoService hoverInfoService,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
            : base(documentContextFactory, languageServerFeatureOptions, documentMappingService, languageServer, loggerFactory.CreateLogger<RazorHoverEndpoint>())
        {
            _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
            _hoverInfoService = hoverInfoService ?? throw new ArgumentNullException(nameof(hoverInfoService));
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string AssociatedServerCapability = "hoverProvider";
            _clientCapabilities = clientCapabilities;

            var registrationOptions = new HoverOptions()
            {
                WorkDoneProgress = false,
            };

            return new RegistrationExtensionResult(AssociatedServerCapability, registrationOptions);
        }

        /// <inheritdoc/>
        protected override string EndpointName => LanguageServerConstants.RazorHoverEndpointName;

        /// <inheritdoc/>
        protected override async Task<DelegatedHoverParams> CreateDelegatedParamsAsync(VSHoverParamsBridge request, CancellationToken cancellationToken)
        {
            var documentContext = await _documentContextFactory.CreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
            var absoluteIndex = request.Position.GetRequiredAbsoluteIndex(sourceText, _logger);
            var projection = await _documentMappingService.GetProjectionAsync(documentContext, absoluteIndex, cancellationToken).ConfigureAwait(false);

            return new DelegatedHoverParams(
                        documentContext.Identifier,
                        projection.Position,
                        projection.LanguageKind);
        }

        /// <inheritdoc/>
        protected override async Task<VSInternalHover?> TryHandleAsync(VSHoverParamsBridge request, CancellationToken cancellationToken)
        {
            var documentContext = await _documentContextFactory.CreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            var projection = await _documentMappingService.TryGetProjectionAsync(documentContext, request.Position, _logger, cancellationToken).ConfigureAwait(false);
            if (projection is null)
            {
                return null;
            }

            if (projection.LanguageKind != RazorLanguageKind.Razor)
            {
                return null;
            }

            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
            var linePosition = new LinePosition(request.Position.Line, request.Position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, request.Position.Line, request.Position.Character);
            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);

            return _hoverInfoService.GetHoverInfo(codeDocument, location, _clientCapabilities!);
        }

        /// <inheritdoc/>
        protected override async Task<VSInternalHover?> HandleDelegatedResponseAsync(VSInternalHover? response, DocumentContext documentContext, CancellationToken cancellationToken)
        {
            if (response is null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.Range is null)
            {
                return response;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);

            if (_documentMappingService.TryMapToProjectedDocumentRange(codeDocument, response.Range, out var projectedRange))
            {
                response.Range = projectedRange;
            }

            return response;
        }
    }
}
