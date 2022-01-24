// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal class RazorBreakpointSpanResponse
    {
        public RazorLanguageKind Kind { get; set; }

        public Range Range{ get; set; }

        public int? HostDocumentVersion { get; set; }
    }
}
