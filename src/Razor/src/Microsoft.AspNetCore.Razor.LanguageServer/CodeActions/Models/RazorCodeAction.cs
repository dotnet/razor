// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    [JsonObject]
    [DebuggerDisplay("{Title,nq}")]
    internal class RazorCodeAction : CodeAction, IRequest<RazorCodeAction>, IBaseRequest
    {
        [JsonProperty(PropertyName = "children")]
        public RazorCodeAction[] Children { get; set; } = Array.Empty<RazorCodeAction>();

        [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }
    }
}
