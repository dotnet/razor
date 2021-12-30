﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using MediatR;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    // Note: This type should be kept in sync with the one in VisualStudio.LanguageServerClient assembly.
    internal class RazorMapToDocumentRangesParams : IRequest<RazorMapToDocumentRangesResponse>
    {
        public RazorLanguageKind Kind { get; set; }

        public Uri RazorDocumentUri { get; set; }

        public Range[] ProjectedRanges { get; set; }

        public MappingBehavior MappingBehavior { get; set; }
    }
}
