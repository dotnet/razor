// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    internal class OmniSharpVSDiagnosticProjectInformation
    {
        /// <summary>
        /// Gets or sets a human-readable identifier for the project in which the diagnostic was generated.
        /// </summary>
        [JsonProperty("_vs_projectName")]
        public string? ProjectName { get; init; }

        /// <summary>
        /// Gets or sets a human-readable identifier for the build context (e.g. Win32 or MacOS)
        /// in which the diagnostic was generated.
        /// </summary>
        [JsonProperty("_vs_context")]
        public string? Context { get; init; }

        /// <summary>
        /// Gets or sets the unique identifier for the project in which the diagnostic was generated.
        /// </summary>
        [JsonProperty("_vs_projectIdentifier")]
        public string? ProjectIdentifier { get; init; }
    }
}
