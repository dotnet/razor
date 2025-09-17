// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Protocol.DevTools;

internal sealed class TagHelpersRequest
{
    [JsonPropertyName("textDocument")]
    public required TextDocumentIdentifier TextDocument { get; set; }

    [JsonPropertyName("kind")]
    public required TagHelpersKind Kind { get; set; }

    public static TagHelpersRequest Create(Uri hostDocumentUri, TagHelpersKind kind)
    {
        return new TagHelpersRequest
        {
            TextDocument = new TextDocumentIdentifier { DocumentUri = new(hostDocumentUri) },
            Kind = kind
        };
    }
}