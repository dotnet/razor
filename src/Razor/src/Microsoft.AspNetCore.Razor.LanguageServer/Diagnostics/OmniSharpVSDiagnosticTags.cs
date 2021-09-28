// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    /// <summary>
    /// Class which contains constant casted values for VS specific DiagnosticTags.
    /// </summary>
    public static class OmniSharpVSDiagnosticTags
    {
        /// <summary>
        /// Error coming from the build.
        /// </summary>
        public const OmniSharpVSDiagnosticTag BuildError = (OmniSharpVSDiagnosticTag)(-1);

        /// <summary>
        /// Error coming from Intellisense.
        /// </summary>
        public const OmniSharpVSDiagnosticTag IntellisenseError = (OmniSharpVSDiagnosticTag)(-2);

        /// <summary>
        /// Diagnostic that could be generated from both builds and
        /// Intellisense.
        ///
        /// Diagnostics tagged with PotentialDuplicate will be hidden
        /// in the error list if:
        ///     The error list is displaying build and intellisense errors.
        ///     The identifier property the containing DiagnosticReport
        ///     matches the supersedes property of another report for the
        ///     same document.
        ///
        /// The latter condition (by itself) will also causes Diagnostics
        /// tagged with the PotentialDuplicate tag to be hidden in the editor.
        /// </summary>
        public const OmniSharpVSDiagnosticTag PotentialDuplicate = (OmniSharpVSDiagnosticTag)(-3);

        /// <summary>
        /// Diagnostic is never displayed in the error list.
        /// </summary>
        public const OmniSharpVSDiagnosticTag HiddenInErrorList = (OmniSharpVSDiagnosticTag)(-4);

        /// <summary>
        /// Diagnostic is always displayed in the error list.
        /// </summary>
        public const OmniSharpVSDiagnosticTag VisibleInErrorList = (OmniSharpVSDiagnosticTag)(-5);

        /// <summary>
        /// Diagnostic is never displayed in the editor.
        /// </summary>
        public const OmniSharpVSDiagnosticTag HiddenInEditor = (OmniSharpVSDiagnosticTag)(-6);

        /// <summary>
        /// No tooltip is shown for the diagnostic in the editor.
        /// </summary>
        public const OmniSharpVSDiagnosticTag SuppressEditorToolTip = (OmniSharpVSDiagnosticTag)(-7);

        /// <summary>
        /// Diagnostic is represented in Editor as an EnC error.
        /// </summary>
        public const OmniSharpVSDiagnosticTag EditAndContinueError = (OmniSharpVSDiagnosticTag)(-8);
    }
}
