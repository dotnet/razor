// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Newtonsoft.Json;
using HoverModel = OmniSharp.Extensions.LanguageServer.Protocol.Models.Hover;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    internal class VSHover : HoverModel
    {
        [JsonProperty("_vs_rawContent")]
        public object? RawContent { get; set; }
    }
}
