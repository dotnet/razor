﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    // NOTE: Changes here MUST be copied over to
    // Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp.RazorMapToDocumentRangesResponse
    internal class RazorMapToDocumentRangesResponse
    {
        public Range[] Ranges { get; set; }

        public int? HostDocumentVersion { get; set; }
    }
}
