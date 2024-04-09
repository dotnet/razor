﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Razor.Protocol.Completion;

internal class RazorInlineCompletionRequest : VSInternalInlineCompletionRequest
{
    [DataMember(Name = "razorLanguageKind")]
    [JsonProperty(Required = Required.Always)]
    public RazorLanguageKind Kind { get; set; }
}
