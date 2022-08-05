// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    internal class RazorHoverEndpoint : AbstractRazorDelegatingEndpoint<VSHoverParamsBridge, VSInternalHover?, DelegatedHoverParams>, IVSHoverEndpoint
    {
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
            : base(documentContextFactory, languageServerFeatureOptions, documentMappingService, languageServer, loggerFactory)
        {
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
        protected override DelegatedHoverParams CreateDelegatedParams(VSHoverParamsBridge request, DocumentContext documentContext, Projection projection)
            => new DelegatedHoverParams(
                documentContext.Identifier,
                projection.Position,
                projection.LanguageKind);

        /// <inheritdoc/>
        protected override Task<VSInternalHover?> HandleInRazorAsync(VSHoverParamsBridge request, DocumentContext documentContext, RazorCodeDocument codeDocument, SourceText sourceText, CancellationToken cancellationToken)
            => DefaultHandleAsync(request, documentContext, codeDocument, sourceText, cancellationToken);

        /// <inheritdoc/>
        protected override Task<VSInternalHover?> HandleWithoutSingleServerAsync(VSHoverParamsBridge request, DocumentContext documentContext, RazorCodeDocument codeDocument, SourceText sourceText, CancellationToken cancellationToken)
            => DefaultHandleAsync(request, documentContext, codeDocument, sourceText, cancellationToken);

        /// <inheritdoc/>
        protected override Task<VSInternalHover?> RemapResponseAsync(VSInternalHover? response, RazorCodeDocument codeDocument, CancellationToken cancellationToken)
        {
            if (response is null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.Range is null)
            {
                return Task.FromResult<VSInternalHover?>(response);
            }

            if (_documentMappingService.TryMapToProjectedDocumentRange(codeDocument, response.Range, out var projectedRange))
            {
                response.Range = projectedRange;
            }

            return Task.FromResult<VSInternalHover?>(response);
        }

        private Task<VSInternalHover?> DefaultHandleAsync(VSHoverParamsBridge request, DocumentContext documentContext, RazorCodeDocument codeDocument, SourceText sourceText, CancellationToken _)
        {
            var linePosition = new LinePosition(request.Position.Line, request.Position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, request.Position.Line, request.Position.Character);
            var result = _hoverInfoService.GetHoverInfo(codeDocument, location, _clientCapabilities!);

            return Task.FromResult(result);
        }
    }
}
