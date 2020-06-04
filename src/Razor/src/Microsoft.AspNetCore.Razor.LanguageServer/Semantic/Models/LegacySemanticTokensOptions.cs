// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    internal class LegacySemanticTokensOptions
    {
        public SemanticTokensLegend Legend { get; set; }

        public bool RangeProvider { get; set; }

        public SemanticTokensDocumentProviderOptions DocumentProvider { get; set; }
    }
}
