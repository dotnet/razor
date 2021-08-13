// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    internal class RazorCompletionContext
    {
        public RazorCompletionContext(
            RazorSyntaxTree syntaxTree,
            TagHelperDocumentContext? tagHelperDocumentContext = null,
            bool isIncompleteRequest = false)
        {
            if (syntaxTree is null) throw new ArgumentNullException(nameof(syntaxTree));

            SyntaxTree = syntaxTree;
            TagHelperDocumentContext = tagHelperDocumentContext;
            IsIncompleteRequest = isIncompleteRequest;
        }

        public bool IsIncompleteRequest { get; }

        public RazorSyntaxTree SyntaxTree { get; }

        public TagHelperDocumentContext? TagHelperDocumentContext { get; }
    }
}
#nullable disable
