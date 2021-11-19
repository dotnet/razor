// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    public record ProvideSemanticTokensRangeParams : SemanticTokensParams
    {
        public long RequiredHostDocumentVersion { get; set; }

        public Range? Range { get; set; }
    }
}
