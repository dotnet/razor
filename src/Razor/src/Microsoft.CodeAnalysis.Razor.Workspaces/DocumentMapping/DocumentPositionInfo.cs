// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

/// <summary>
/// Represents a position in a document. If <see cref="LanguageKind"/> is Razor then the position will be
/// in the host document, otherwise it will be in the corresponding generated document.
/// </summary>
[DataContract]
internal record struct DocumentPositionInfo
{
    [DataMember(Name = "languageKind")]
    [JsonPropertyName("languageKind")]
    public required RazorLanguageKind LanguageKind { get; set; }

    [DataMember(Name = "position")]
    [JsonPropertyName("position")]
    public required Position Position { get; set; }

    [DataMember(Name = "hostDocumentIndex")]
    [JsonPropertyName("hostDocumentIndex")]
    public required int HostDocumentIndex { get; set; }
}
