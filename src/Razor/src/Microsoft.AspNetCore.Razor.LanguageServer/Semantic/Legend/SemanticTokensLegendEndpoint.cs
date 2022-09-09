// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Legend
{
    // Used exclusively in VSCode to initialize semantic tokens
    internal class RazorSemanticTokensLegendEndpoint : IRazorSemanticTokensLegendHandler
    {
        private readonly ILogger<RazorSemanticTokensEndpoint> _logger;

        public RazorSemanticTokensLegendEndpoint(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<RazorSemanticTokensEndpoint>();
        }

        public Task<SemanticTokensLegend> Handle(SemanticTokensLegendParams request, CancellationToken cancellationToken)
        {
            return Task.FromResult(RazorSemanticTokensLegend.Instance);
        }
    }
}
