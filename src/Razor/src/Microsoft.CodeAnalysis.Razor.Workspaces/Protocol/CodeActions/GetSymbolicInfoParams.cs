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

    [DataMember(Name = "project")]
    [JsonPropertyName("project")]
    public required TextDocumentIdentifier Project { get; set; }

    [DataMember(Name = "hostDocumentVersion")]
    [JsonPropertyName("hostDocumentVersion")]
    public required int HostDocumentVersion { get; set; }

    [DataMember(Name = "generatedDocumentRanges")]
    [JsonPropertyName("generatedDocumentRanges")]
    public required Range[] GeneratedDocumentRanges { get; set; }
}

internal sealed record MemberSymbolicInfo
{
    public required MethodSymbolicInfo[] Methods { get; set; }
    public required AttributeSymbolicInfo[] Attributes { get; set; }
}

internal sealed record MethodSymbolicInfo
{
    public required string Name { get; set; }

    public required string ReturnType { get; set; }

    public required string[] ParameterTypes { get; set; }
}

internal sealed record AttributeSymbolicInfo
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public required bool IsValueType { get; set; }
    public required bool IsWrittenTo { get; set; }
}
