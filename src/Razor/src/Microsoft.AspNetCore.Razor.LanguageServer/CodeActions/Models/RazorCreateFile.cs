// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    [JsonObject]
    internal class RazorCreateFile : CreateFile
    {
        // RazorCreateFile inherits from CreateFile to ensure we can utilize the O# CreateFile
        // Re-implements the URI property to be a Uri type instead of Uri string.

        [JsonProperty(PropertyName = "uri", Required = Required.Always)]
        public new Uri Uri { get; set; }
    }
}
