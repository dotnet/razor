// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Razor.NestedFiles;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

/// <summary>
/// Parameters for the razor/addNestedFile endpoint.
/// </summary>
internal sealed class AddNestedFileParams
{
    /// <summary>
    /// The URI of the Razor file (.razor or .cshtml) to create a nested file for.
    /// </summary>
    [JsonPropertyName("razorFileUri")]
    public required Uri RazorFileUri { get; set; }

    /// <summary>
    /// The kind of nested file to create (one of <see cref="NestedFileKind"/> constants).
    /// </summary>
    [JsonPropertyName("fileKind")]
    public required string FileKind { get; set; }
}
