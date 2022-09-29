﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    // NOTE: Changes here MUST be copied over to
    // Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp.RazorMapToDocumentRangesResponse
    internal class RazorMapToDocumentRangesResponse
    {
        public required Range[] Ranges { get; set; }

        public int? HostDocumentVersion { get; set; }
    }
}
