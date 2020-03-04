// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    public class RazorSemanticTokenLegendEndpoint : ISemanticTokenLegendHandler
    {
        public Task<SemanticTokenLegend> Handle(SemanticTokenLegendParams request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokenLegend());
        }

        public void SetCapability(SemanticTokenLegendCapability capability)
        {
            throw new NotImplementedException();
        }
    }
}
