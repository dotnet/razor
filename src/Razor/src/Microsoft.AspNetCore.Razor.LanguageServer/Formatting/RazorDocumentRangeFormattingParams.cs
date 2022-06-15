// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class RazorDocumentRangeFormattingParams : IRequest<RazorDocumentFormattingResponse>
    {
        public RazorLanguageKind Kind { get; set; }

        public string? HostDocumentFilePath { get; set; }

        public Range? ProjectedRange { get; set; }

        public FormattingOptions? Options { get; set; }
    }
}
