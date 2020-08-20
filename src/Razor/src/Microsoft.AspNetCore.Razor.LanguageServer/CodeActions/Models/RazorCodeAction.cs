// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    [JsonObject]
    public class RazorCodeAction : CodeAction, IRequest<RazorCodeAction>, IBaseRequest
    {
        public RazorCodeAction()
        {
        }

        [JsonProperty(PropertyName = "title", Required = Required.Always)]
        public new string Title { get; set; }

        [JsonProperty(PropertyName = "kind", NullValueHandling = NullValueHandling.Ignore)]
        public new CodeActionKind Kind { get; set; }

        [JsonProperty(PropertyName = "diagnostics", NullValueHandling = NullValueHandling.Ignore)]
        public new Container<Diagnostic> Diagnostics { get; set; }

        [JsonProperty(PropertyName = "edit", NullValueHandling = NullValueHandling.Ignore)]
        public new WorkspaceEdit Edit { get; set; }

        [JsonProperty(PropertyName = "command", NullValueHandling = NullValueHandling.Ignore)]
        public new Command Command { get; set; }

        [JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; set; }

        [JsonProperty(PropertyName = "children")]
        public RazorCodeAction[] Children { get; set; } = Array.Empty<RazorCodeAction>();
    }
}
