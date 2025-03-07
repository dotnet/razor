// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

internal sealed class RazorDocumentDiagnosticParams
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("diagnosticParams")]
    public required DocumentDiagnosticParams DiagnosticParams { get; set; }
}
