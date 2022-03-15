// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.VisualStudio.Editor.Razor
{
    public class AttributeCompletionContext
    {
        public AttributeCompletionContext(
            TagHelperDocumentContext documentContext!!,
            IEnumerable<string> existingCompletions!!,
            string currentTagName!!,
            string currentAttributeName,
            IEnumerable<KeyValuePair<string, string>> attributes!!,
            string currentParentTagName,
            bool currentParentIsTagHelper,
            Func<string, bool> inHTMLSchema!!)
        {
            DocumentContext = documentContext;
            ExistingCompletions = existingCompletions;
            CurrentTagName = currentTagName;
            CurrentAttributeName = currentAttributeName;
            Attributes = attributes;
            CurrentParentTagName = currentParentTagName;
            CurrentParentIsTagHelper = currentParentIsTagHelper;
            InHTMLSchema = inHTMLSchema;
        }

        public TagHelperDocumentContext DocumentContext { get; }

        public IEnumerable<string> ExistingCompletions { get; }

        public string CurrentTagName { get; }

        public string CurrentAttributeName { get; }

        public IEnumerable<KeyValuePair<string, string>> Attributes { get; }

        public string CurrentParentTagName { get; }

        public bool CurrentParentIsTagHelper { get; }

        public Func<string, bool> InHTMLSchema { get; }
    }
}