// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    /// <summary>
    /// Class representing the parameters sent for a textDocument/_vs_textPresentation request, plus
    /// a host document version.
    /// </summary>
    internal class RazorTextPresentationParams : TextPresentationParams, IRazorPresentationParams
    {
        public RazorLanguageKind Kind { get; set; }

        public int HostDocumentVersion { get; set; }

        public RazorTextPresentationParams(TextDocumentIdentifier textDocument, Range range)
            : base(textDocument, range)
        {
        }
    }
}
