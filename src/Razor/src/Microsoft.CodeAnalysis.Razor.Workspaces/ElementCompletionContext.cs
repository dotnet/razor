// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.VisualStudio.Editor.Razor
{
    public sealed class ElementCompletionContext
    {
        public ElementCompletionContext(
            TagHelperDocumentContext documentContext,
            IEnumerable<string> existingCompletions,
            string containingTagName,
            IEnumerable<KeyValuePair<string, string>> attributes,
            string containingParentTagName,
            bool containingParentIsTagHelper,
            Func<string, bool> inHTMLSchema)
        {
            if (documentContext is null)
            {
                throw new ArgumentNullException(nameof(documentContext));
            }

            if (existingCompletions is null)
            {
                throw new ArgumentNullException(nameof(existingCompletions));
            }

            if (inHTMLSchema is null)
            {
                throw new ArgumentNullException(nameof(inHTMLSchema));
            }

            DocumentContext = documentContext;
            ExistingCompletions = existingCompletions;
            ContainingTagName = containingTagName;
            Attributes = attributes;
            ContainingParentTagName = containingParentTagName;
            ContainingParentIsTagHelper = containingParentIsTagHelper;
            InHTMLSchema = inHTMLSchema;
        }

        public TagHelperDocumentContext DocumentContext { get; }

        public IEnumerable<string> ExistingCompletions { get; }

        public string ContainingTagName { get; }

        public IEnumerable<KeyValuePair<string, string>> Attributes { get; }

        public string ContainingParentTagName { get; }

        public bool ContainingParentIsTagHelper { get; }

        public Func<string, bool> InHTMLSchema { get; }
    }
}
