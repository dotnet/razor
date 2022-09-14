﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.Extensions.Internal;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    // Note: This type should be kept in sync with the one in Razor.LanguageServer assembly.
    internal class RazorMapToDocumentRangesParams : IEquatable<RazorMapToDocumentRangesParams>
    {
        public RazorLanguageKind Kind { get; set; }

        public Uri RazorDocumentUri { get; set; }

        public Range[] ProjectedRanges { get; set; }

        public LanguageServerMappingBehavior MappingBehavior { get; set; }

        public bool Equals(RazorMapToDocumentRangesParams other)
        {
            return
                other is not null &&
                Kind == other.Kind &&
                RazorDocumentUri == other.RazorDocumentUri &&
                MappingBehavior == other.MappingBehavior &&
                Enumerable.SequenceEqual(ProjectedRanges, other.ProjectedRanges);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RazorMapToDocumentRangesParams);
        }

        public override int GetHashCode()
        {
            var hash = new HashCodeCombiner();
            hash.Add(Kind);
            hash.Add(RazorDocumentUri);
            hash.Add(ProjectedRanges);
            hash.Add(MappingBehavior);
            return hash;
        }
    }
}
