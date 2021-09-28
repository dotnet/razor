// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using Newtonsoft.Json;
using HoverModel = OmniSharp.Extensions.LanguageServer.Protocol.Models.Hover;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    internal record OmniSharpVSHover : HoverModel
    {
        [JsonProperty("_vs_rawContent")]
        public object? RawContent { get; init; }
    }
}
