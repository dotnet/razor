// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
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
        private VSInternalClientCapabilities? _clientCapabilities;

        public RazorHoverEndpoint(
            DocumentContextFactory documentContextFactory,
            RazorHoverInfoService hoverInfoService,
            ILoggerFactory loggerFactory)
        {
            if (documentContextFactory is null)
            {
                throw new ArgumentNullException(nameof(documentContextFactory));
            }

            if (hoverInfoService is null)
            {
                throw new ArgumentNullException(nameof(hoverInfoService));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _documentContextFactory = documentContextFactory;
            _hoverInfoService = hoverInfoService;
            _logger = loggerFactory.CreateLogger<RazorHoverEndpoint>();
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

            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken);
            var linePosition = new LinePosition((int)request.Position.Line, (int)request.Position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, (int)request.Position.Line, (int)request.Position.Character);

            var hoverItem = _hoverInfoService.GetHoverInfo(codeDocument, location, _clientCapabilities!);

            _logger.LogTrace($"Found hover info items.");

            return hoverItem;
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
