// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.
#pragma warning disable CS0618
#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class RazorSemanticTokensLegendEndpoint : ISemanticTokensLegendHandler
    {
        public Task<SemanticTokensLegend> Handle(SemanticTokensLegendParams request, CancellationToken cancellationToken)
        {
            return Task.FromResult(RazorSemanticTokensLegend.Instance);
        }
    }
}
