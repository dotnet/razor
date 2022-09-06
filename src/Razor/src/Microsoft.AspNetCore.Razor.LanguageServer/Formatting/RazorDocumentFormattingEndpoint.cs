// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class RazorDocumentFormattingEndpoint : IVSDocumentFormattingEndpoint
    {
        private readonly DocumentContextFactory _documentContextFactory;
        private readonly RazorFormattingService _razorFormattingService;
        private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor;

        public RazorDocumentFormattingEndpoint(
            DocumentContextFactory documentContextFactory,
            RazorFormattingService razorFormattingService,
            IOptionsMonitor<RazorLSPOptions> optionsMonitor)
        {
            if (documentContextFactory is null)
            {
                throw new ArgumentNullException(nameof(documentContextFactory));
            }

            if (razorFormattingService is null)
            {
                throw new ArgumentNullException(nameof(razorFormattingService));
            }

            if (optionsMonitor is null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            _documentContextFactory = documentContextFactory;
            _razorFormattingService = razorFormattingService;
            _optionsMonitor = optionsMonitor;
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            // VSCode registers built-in features by default:
            // https://github.com/microsoft/vscode-languageserver-node/blob/ed6a6d7da0ad64ebea0b55e4b2f339a1ec7f511f/client/src/common/client.ts#L1615
            if (!clientCapabilities.SupportsVisualStudioExtensions)
            {
                return null;
            }

            const string ServerCapability = "documentFormattingProvider";

            return new RegistrationExtensionResult(ServerCapability, new DocumentFormattingOptions());
        }

        public async Task<TextEdit[]?> Handle(DocumentFormattingParamsBridge request, CancellationToken cancellationToken)
        {
            if (!_optionsMonitor.CurrentValue.EnableFormatting)
            {
                return null;
            }

            var documentContext = await _documentContextFactory.TryCreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var edits = await _razorFormattingService.FormatAsync(request.TextDocument.Uri, documentContext.Snapshot, range: null, request.Options, cancellationToken);
            return edits;
        }
    }
}
