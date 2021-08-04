// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    // Note: This type should be kept in sync with the one in VisualStudio.LanguageServerClient assembly.
    internal class RazorDiagnosticsResponse
    {
        public OmniSharpVSDiagnostic[] Diagnostics { get; set; }

        public int? HostDocumentVersion { get; set; }
    }
}
