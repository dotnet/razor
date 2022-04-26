// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using System.Diagnostics;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class RazorSemanticTokensEndpoint : ISemanticTokensRangeHandler
    {
        private readonly ILogger _logger;
        private readonly RazorSemanticTokensInfoService _semanticTokensInfoService;

        public RazorSemanticTokensEndpoint(
            RazorSemanticTokensInfoService semanticTokensInfoService!!,
            ILoggerFactory loggerFactory!!)
        {
            _semanticTokensInfoService = semanticTokensInfoService;
            _logger = loggerFactory.CreateLogger<RazorSemanticTokensEndpoint>();
        }

        public async Task<SemanticTokens?> Handle(SemanticTokensRangeParams request!!, CancellationToken cancellationToken)
        {
            var semanticTokens = await _semanticTokensInfoService.GetSemanticTokensAsync(request.TextDocument, request.Range, cancellationToken);
            var amount = semanticTokens is null ? "no" : (semanticTokens.Data.Length / 5).ToString(Thread.CurrentThread.CurrentCulture);

            _logger.LogInformation($"Returned {amount} semantic tokens for range {request.Range} in {request.TextDocument.Uri}.");

            if (semanticTokens is not null)
            {
                Debug.Assert(semanticTokens.Data.Length % 5 == 0, $"Number of semantic token-ints should be divisible by 5. Actual number: {semanticTokens.Data.Length}");
                Debug.Assert(semanticTokens.Data.Length == 0 || semanticTokens.Data[0] >= 0, $"Line offset should not be negative.");
            }

            return semanticTokens;
        }

        public SemanticTokensRegistrationOptions GetRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector,
                Full = false,
                Legend = RazorSemanticTokensLegend.Instance,
                Range = true,
            };
        }
    }
}
