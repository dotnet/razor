// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    // Note: This type should be kept in sync with the one in VisualStudio.LanguageServerClient assembly.
    internal class RazorMapToDocumentRangesParams : IRequest<RazorMapToDocumentRangesResponse>
    {
        public RazorLanguageKind Kind { get; init; }

        public required Uri RazorDocumentUri { get; init; }

        public required Range[] ProjectedRanges { get; init; }

        public MappingBehavior MappingBehavior { get; init; }
    }
}
