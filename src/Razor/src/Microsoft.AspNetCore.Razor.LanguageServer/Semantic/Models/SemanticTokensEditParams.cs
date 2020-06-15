// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Interfaces
{
    public class SemanticTokensEditParams : IRequest<SemanticTokensOrSemanticTokensEdits?>
    {
        public RazorLanguageKind Kind { get; set; }
        public Uri RazorDocumentUri { get; set; }
        public string PreviousResultId { get; set; }
    }
}
