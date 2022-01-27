// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

/// <summary>
/// Corresponds to https://devdiv.visualstudio.com/DevDiv/_git/VSLanguageServerClient?path=/src/product/Protocol/LanguageServer.Protocol.Internal/VSInternalInlineCompletionContext.cs
/// </summary>
public class InlineCompletionContext
{
    [DataMember(Name = "_vs_triggerKind")]
    [JsonProperty(Required = Required.Always)]
    public InlineCompletionTriggerKind TriggerKind { get; set; } = InlineCompletionTriggerKind.Explicit;

    [DataMember(Name = "_vs_selectedCompletionInfo")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public SelectedCompletionInfo? SelectedCompletionInfo { get; set; }
}
