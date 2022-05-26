// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    // Note: This type should be kept in sync with the one in VisualStudio.LanguageServerClient assembly.
    internal class RazorMapToDocumentRangesParams : IRequest<RazorMapToDocumentRangesResponse>
    {
        public RazorLanguageKind Kind { get; set; }

        public Uri RazorDocumentUri { get; set; }

        public VS.Range[] ProjectedRanges { get; set; }

        public MappingBehavior MappingBehavior { get; set; }
    }
}
