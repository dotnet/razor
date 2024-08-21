// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;

[DataContract]
internal record GetSymbolicInfoParams
{
    [DataMember(Name = "document")]
    [JsonPropertyName("document")]
    public required TextDocumentIdentifier Document { get; set; }

    [DataMember(Name = "newDocument")]
    [JsonPropertyName("newDocument")]
    public required TextDocumentIdentifier NewDocument { get; set; }

    [DataMember(Name = "project")]
    [JsonPropertyName("project")]
    public required TextDocumentIdentifier Project { get; set; }

    [DataMember(Name = "hostDocumentVersion")]
    [JsonPropertyName("hostDocumentVersion")]
    public required int HostDocumentVersion { get; set; }

    [DataMember(Name = "newContents")]
    [JsonPropertyName("newContents")]
    public required string NewContents { get; set; }

    [DataMember(Name = "mappedRange")]
    [JsonPropertyName("mappedRange")]
    public required Range MappedRange { get; set; }

    [DataMember(Name = "intersectingSpansInGeneratedRange")]
    [JsonPropertyName("intersectingSpansInGeneratedRange")]

    public required Range[] IntersectingSpansInGeneratedMappings { get; set; }
}

internal sealed record SymbolicInfo
{
    public required MethodInRazorInfo[] Methods { get; set; }
    public required SymbolInRazorInfo[] Fields { get; set; }
}

internal sealed record MethodInRazorInfo
{
    public required string Name { get; set; }

    public required string ReturnType { get; set; }

    public required string[] ParameterTypes { get; set; }
}

internal sealed record SymbolInRazorInfo
{
    public required string Name { get; set; }
    public required string Type { get; set; }
}
