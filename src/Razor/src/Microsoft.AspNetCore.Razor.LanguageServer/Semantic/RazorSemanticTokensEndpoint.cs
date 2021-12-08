// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class RazorSemanticTokensEndpoint : ISemanticTokensFullHandler, ISemanticTokensRangeHandler, ISemanticTokensDeltaHandler
    {
        private readonly ILogger _logger;
        private readonly RazorSemanticTokensInfoService _semanticTokensInfoService;

        public RazorSemanticTokensEndpoint(
            RazorSemanticTokensInfoService semanticTokensInfoService,
            ILoggerFactory loggerFactory)
        {
            if (semanticTokensInfoService is null)
            {
                throw new ArgumentNullException(nameof(semanticTokensInfoService));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _semanticTokensInfoService = semanticTokensInfoService;
            _logger = loggerFactory.CreateLogger<RazorSemanticTokensEndpoint>();
        }

        public async Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return await HandleAsync(request.TextDocument.Uri.GetAbsolutePath(), cancellationToken, range: null);
        }

        public async Task<SemanticTokens?> Handle(SemanticTokensRangeParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var semanticTokens = await HandleAsync(request.TextDocument, cancellationToken, request.Range);
            var amount = semanticTokens is null ? "no" : (semanticTokens.Data.Length / 5).ToString(Thread.CurrentThread.CurrentCulture);

            _logger.LogInformation($"Returned {amount} semantic tokens for range {request.Range} in {request.TextDocument.Uri}.");

            return semanticTokens;
        }

        public async Task<SemanticTokensFullOrDelta?> Handle(SemanticTokensDeltaParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var edits = await _semanticTokensInfoService.GetSemanticTokensEditsAsync(request.TextDocument, request.PreviousResultId, cancellationToken);

            return edits;
        }

        public SemanticTokensRegistrationOptions GetRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector,
                Full = new SemanticTokensCapabilityRequestFull
                {
                    Delta = true,
                },
                Legend = RazorSemanticTokensLegend.Instance,
                Range = false,
            };
        }

        private async Task<SemanticTokens?> HandleAsync(TextDocumentIdentifier textDocument, CancellationToken cancellationToken, Range? range = null)
        {
            var tokens = await _semanticTokensInfoService.GetSemanticTokensAsync(textDocument, range, cancellationToken);

            return tokens;
        }
    }
}
