﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Models;

[DataContract]
internal sealed class RazorVSInternalCodeAction : VSInternalCodeAction
{
    [JsonPropertyName("name")]
    [DataMember(Name = "name")]
    public string? Name { get; set; }
}
