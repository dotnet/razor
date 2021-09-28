// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

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
