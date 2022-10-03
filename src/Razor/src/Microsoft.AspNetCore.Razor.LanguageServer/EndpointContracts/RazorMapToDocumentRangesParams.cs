// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    // Note: This type should be kept in sync with the one in VisualStudio.LanguageServerClient assembly.
    internal class RazorMapToDocumentRangesParams
    {
        public RazorLanguageKind Kind { get; set; }

        public required Uri RazorDocumentUri { get; set; }

        public required Range[] ProjectedRanges { get; set; }

        public MappingBehavior MappingBehavior { get; set; }
    }
}
