// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Razor.IsolationFiles;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

/// <summary>
/// Parameters for the razor/addIsolationFile endpoint.
/// </summary>
internal sealed class AddIsolationFileParams
{
    /// <summary>
    /// The URI of the Razor file (.razor or .cshtml) to create an isolation file for.
    /// </summary>
    [JsonPropertyName("razorFileUri")]
    public required Uri RazorFileUri { get; set; }

    /// <summary>
    /// The kind of isolation file to create (one of <see cref="IsolationFileKind"/> constants).
    /// </summary>
    [JsonPropertyName("fileKind")]
    public required string FileKind { get; set; }
}
