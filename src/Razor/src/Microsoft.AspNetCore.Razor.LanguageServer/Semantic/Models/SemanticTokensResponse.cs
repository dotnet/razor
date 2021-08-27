// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    internal record SemanticTokensResponse
    {
        public string? ResultId { get; set; }

        public int[] Data { get; set; } = Array.Empty<int>();

        public bool IsPartial { get; set; }
    }
}
