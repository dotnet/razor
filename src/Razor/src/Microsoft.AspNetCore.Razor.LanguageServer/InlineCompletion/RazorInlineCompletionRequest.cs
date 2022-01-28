// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class RazorInlineCompletionRequest : InlineCompletionRequest
{
    [DataMember(Name = "razorLanguageKind")]
    [JsonProperty(Required = Required.Always)]
    public RazorLanguageKind Kind { get; set; }
}
