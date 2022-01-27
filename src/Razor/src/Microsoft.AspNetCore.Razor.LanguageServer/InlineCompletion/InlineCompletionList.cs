// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class InlineCompletionList
{
    /// <summary>
    /// Gets or sets the inline completion items.
    /// </summary>
    [DataMember(Name = "_vs_items")]
    [JsonProperty(Required = Required.Always)]
    public InlineCompletionItem[] Items { get; set; }
}
