// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;

internal sealed record RazorComponentInfoParams
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

    [DataMember(Name = "scanRange")]
    [JsonPropertyName("scanRange")]
    public required Range ScanRange { get; init; }
}

// Not sure where to put these two records
internal sealed record RazorComponentInfo
{
    public required HashSet<MethodInsideRazorElementInfo>? Methods { get; init; }
    public required HashSet<ISymbol>? Fields { get; init; }
}

internal sealed record MethodInsideRazorElementInfo
{
    public required string Name { get; set; }

    public required string ReturnType { get; set; }

    public required List<string>? ParameterTypes { get; set; }
}
