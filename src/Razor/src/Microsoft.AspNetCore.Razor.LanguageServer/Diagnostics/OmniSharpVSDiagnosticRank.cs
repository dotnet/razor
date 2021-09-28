// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    /// <summary>
    /// Enum which represents the rank of a diagnostic.
    /// </summary>
    public enum OmniSharpVSDiagnosticRank
    {
        /// <summary>
        /// Highest priority.
        /// </summary>
        Highest = 100,

        /// <summary>
        /// High priority.
        /// </summary>
        High = 200,

        /// <summary>
        /// Default priority.
        /// </summary>
        Default = 300,

        /// <summary>
        /// Low priority.
        /// </summary>
        Low = 400,

        /// <summary>
        /// Lowest priority.
        /// </summary>
        Lowest = 500,
    }
}
