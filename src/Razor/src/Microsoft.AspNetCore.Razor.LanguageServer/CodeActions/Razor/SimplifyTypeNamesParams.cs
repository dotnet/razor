﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

//
// Summary:
//     Class representing the parameters sent from the client to the server for the
//     textDocument/simplifyTypeNames request.
[DataContract]
internal record SimplifyTypeNamesParams : ITextDocumentParams
{
    //
    // Summary:
    //     Gets or sets the value which identifies the document.
    [DataMember(Name = "textDocument")]
    public required TextDocumentIdentifier TextDocument { get; set; }

    //
    // Summary:
    //     Gets or sets the value which identifies the document the simplified types will be placed in,
    //     if not the same as the document the request came from.
    [DataMember(Name = "placementTextDocument")]
    public OptionalVersionedTextDocumentIdentifier? PlacementTextDocument { get; set; }

    //
    // Summary:
    //     Gets or sets fully qualified type names to be simplified.
    [DataMember(Name = "fullyQualifiedTypeNames")]
    public required string[] FullyQualifiedTypeNames { get; set; }

    //
    // Summary:
    //     Gets or sets the value which indicates the position within the document the simplified types will be placed.
    [DataMember(Name = "absoluteIndex")]
    public required int AbsoluteIndex { get; set; }
}
