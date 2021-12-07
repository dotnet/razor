// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    /// <summary>
    /// Diagnostic tag enum.
    /// Additional metadata about the type of a diagnostic
    /// </summary>
    public enum OmniSharpVSDiagnosticTag
    {
        /// <summary>
        /// Unused or unnecessary code.
        /// Diagnostics with this tag are rendered faded out.
        /// </summary>
        Unnecessary = 1,

        /// <summary>
        /// Deprecated or obsolete code.
        /// Clients are allowed to rendered diagnostics with this tag strike through.
        /// </summary>
        Deprecated = 2,
    }
}
