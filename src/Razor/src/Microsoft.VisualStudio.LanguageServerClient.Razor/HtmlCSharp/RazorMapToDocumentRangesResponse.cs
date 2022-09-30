// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    // Note: This type should be kept in sync with the one in Razor.LanguageServer assembly.
    internal class RazorMapToDocumentRangesResponse
    {
        public required Range[] Ranges { get; init; }

        public int? HostDocumentVersion { get; init; }
    }
}
