// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal abstract class RazorSemanticTokenInfoService
    {
        public abstract SemanticTokens GetSemanticTokens(RazorCodeDocument codeDocument, Range range = null);

        public abstract SemanticTokensOrSemanticTokensEdits GetSemanticTokenEdits(RazorCodeDocument codeDocument, string previousId);
    }
}
