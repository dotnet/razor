// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

#nullable enable

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    internal record OmniSharpVSDiagnostic : Diagnostic
    {
        public static readonly PlatformExtensionConverter<Diagnostic, OmniSharpVSDiagnostic> JsonConverter = new();

        // We need to override the "Tags" property because the basic Diagnostic Tags property has a custom JsonConverter that does not allow
        // VS extensions to tags.
        public new Container<OmniSharpVSDiagnosticTag>? Tags { get; init; }

        /// <summary>
        /// Gets or sets the project and context (e.g. Win32, MacOS, etc.) in which the diagnostic was generated.
        /// </summary>
        [JsonProperty("_vs_projects")]
        public OmniSharpVSDiagnosticProjectInformation[]? Projects { get; init; }

        /// <summary>
        /// Gets or sets an expanded description of the diagnostic.
        /// </summary>
        [JsonProperty("_vs_expandedMessage")]
        public string? ExpandedMessage { get; init; }

        /// <summary>
        /// Gets or sets a message shown when the user hovers over an error. If null, then message
        /// is used (use <see cref="OmniSharpVSDiagnosticTags.SuppressEditorToolTip"/> to prevent a tool tip from being shown).
        /// </summary>
        [JsonProperty("_vs_toolTip")]
        public string? ToolTip { get; init; }

        /// <summary>
        /// Gets or sets some non-human readable identifier so that two diagnostics that are
        /// equivalent (e.g.a syntax error in both a build for Win32 and MacOs) can be consolidated.
        /// </summary>
        [JsonProperty("_vs_identifier")]
        public string? Identifier { get; init; }

        /// <summary>
        /// Gets or sets a string describing the diagnostic types (e.g. Security, Performance, Style, ...).
        /// </summary>
        [JsonProperty("_vs_diagnosticType")]
        public string? DiagnosticType { get; init; }

        /// <summary>
        /// Gets or sets a rank associated with this diagnostic, used for the default sort.
        /// Default == 300 will be used if no rank is specified.
        /// </summary>
        [JsonProperty("_vs_diagnosticRank")]
        public OmniSharpVSDiagnosticRank? DiagnosticRank { get; init; }

        /// <summary>
        /// Gets or sets an ID used to associate this diagnostic with a corresponding line in the output window.
        /// </summary>
        [JsonProperty("_vs_outputId")]
        public int? OutputId { get; init; }
    }
}
