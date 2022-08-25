// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorMapToDocumentEditsParams : IRequest<RazorMapToDocumentEditsResponse>
    {
        public RazorLanguageKind Kind { get; set; }

        public required Uri RazorDocumentUri { get; set; }

        public required TextEdit[] ProjectedTextEdits { get; set; }

        public TextEditKind TextEditKind { get; set; }

        public required FormattingOptions FormattingOptions { get; set; }
    }
}
