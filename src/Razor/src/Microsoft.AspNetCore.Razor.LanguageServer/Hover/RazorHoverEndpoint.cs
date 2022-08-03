// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    internal class RazorHoverEndpoint : IVSHoverEndpoint
    {
        private readonly ILogger _logger;
        private readonly DocumentContextFactory _documentContextFactory;
        private readonly RazorHoverInfoService _hoverInfoService;
        private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ClientNotifierServiceBase _languageServer;
        private VSInternalClientCapabilities? _clientCapabilities;

        public RazorHoverEndpoint(
            DocumentContextFactory documentContextFactory,
            RazorHoverInfoService hoverInfoService,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
        {
            _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
            _hoverInfoService = hoverInfoService ?? throw new ArgumentNullException(nameof(hoverInfoService));
            _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
            _logger = loggerFactory?.CreateLogger<RazorHoverEndpoint>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public async Task<VSInternalHover?> Handle(VSHoverParamsBridge request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var documentContext = await _documentContextFactory.TryCreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                return null;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            // If we're not doing single server rename, then we're done. C# and Html will be handled by the RenameHandler in the HtmlCSharp server.
            if (!_languageServerFeatureOptions.SingleServerSupport)
            {
                return null;
            }

            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
            if (!request.Position.TryGetAbsoluteIndex(sourceText, _logger, out var absoluteIndex))
            {
                return null;
            }

            var projection = await _documentMappingService.GetProjectionAsync(documentContext, absoluteIndex, cancellationToken).ConfigureAwait(false);

            // If the language is Razor then the downstream servers won't know how to handle it anyway
            if (projection.LanguageKind == Protocol.RazorLanguageKind.Razor)
            {
                return null;
            }

            var delegatedParams = new DelegatedHoverParams(
                documentContext.Identifier,
                projection.Position,
                projection.LanguageKind);

            var delegatedRequest = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorHoverEndpointName, delegatedParams).ConfigureAwait(false);
            var delegatedResponse = await delegatedRequest.Returning<VSInternalHover?>(cancellationToken).ConfigureAwait(false);

            if (delegatedResponse is not null &&
                delegatedResponse.Range is not null &&
                _documentMappingService.TryMapToProjectedDocumentRange(codeDocument, delegatedResponse.Range, out var projectedRange))
            {
                delegatedResponse.Range = projectedRange;
            }

            return delegatedResponse;
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
    }
}
