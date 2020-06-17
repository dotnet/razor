// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class SyntaxResult
    {
        public SyntaxResult(SyntaxNode node, SyntaxKind kind, RazorCodeDocument razorCodeDocument)
        {
            var range = node.GetRange(razorCodeDocument.Source);
            Range = range;
            Kind = kind;
        }

        public Range Range { get; set; }

        public SyntaxKind Kind { get; set; }
    }
}
