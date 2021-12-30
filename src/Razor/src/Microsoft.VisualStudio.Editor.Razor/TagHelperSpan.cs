﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.VisualStudio.Editor.Razor
{
    public struct TagHelperSpan
    {
        public TagHelperSpan(SourceSpan span, TagHelperBinding binding)
        {
            if (binding is null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            Span = span;
            Binding = binding;
        }

        public TagHelperBinding Binding { get; }

        public IEnumerable<TagHelperDescriptor> TagHelpers => Binding.Descriptors;

        public SourceSpan Span { get; }
    }
}
