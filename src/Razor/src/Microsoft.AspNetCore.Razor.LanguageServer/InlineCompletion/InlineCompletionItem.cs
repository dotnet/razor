// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Runtime.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

/// <summary>
/// Corresponds to https://devdiv.visualstudio.com/DevDiv/_git/VSLanguageServerClient?path=/src/product/Protocol/LanguageServer.Protocol.Internal/VSInternalInlineCompletionItem.cs
/// </summary>
internal class InlineCompletionItem
{
    [DataMember(Name = "_vs_text")]
    [JsonProperty(Required = Required.Always)]
    public string Text { get; set; }

    [DataMember(Name = "_vs_range")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public Range Range { get; set; }

    [DataMember(Name = "_vs_command")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public Command Command { get; set; }

    [DataMember(Name = "_vs_insertTextFormat")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public InsertTextFormat? TextFormat { get; set; } = InsertTextFormat.PlainText;
}
