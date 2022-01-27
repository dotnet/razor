// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class InlineCompletionOptions : ITextDocumentRegistrationOptions
{
    /// <summary>
    /// Gets or sets a regex used by the client to determine when to ask the server for snippets.
    /// </summary>
    [DataMember(Name = "_vs_pattern")]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string Pattern { get; set; }

    public DocumentSelector DocumentSelector { get; set; }
}
