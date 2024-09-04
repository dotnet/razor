// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.CodeAnalysis.Razor.Protocol;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

internal static class LanguageKindHelper
{
    public static ImmutableArray<ClassifiedSpanInternal> GetClassifiedSpans(RazorCodeDocument document)
    {
        // Since this service is called so often, we get a good performance improvement by caching these values
        // for this code document. If the document changes, as the user types, then the document instance will be
        // different, so we don't need to worry about invalidating the cache.
        if (!document.Items.TryGetValue(typeof(ClassifiedSpanInternal), out ImmutableArray<ClassifiedSpanInternal> classifiedSpans))
        {
            var syntaxTree = document.GetSyntaxTree();
            classifiedSpans = ClassifiedSpanVisitor.VisitRoot(syntaxTree);

            document.Items[typeof(ClassifiedSpanInternal)] = classifiedSpans;
        }

        return classifiedSpans;
    }

    public static ImmutableArray<TagHelperSpanInternal> GetTagHelperSpans(RazorCodeDocument document)
    {
        // Since this service is called so often, we get a good performance improvement by caching these values
        // for this code document. If the document changes, as the user types, then the document instance will be
        // different, so we don't need to worry about invalidating the cache.
        if (!document.Items.TryGetValue(typeof(TagHelperSpanInternal), out ImmutableArray<TagHelperSpanInternal> tagHelperSpans))
        {
            var syntaxTree = document.GetSyntaxTree();
            tagHelperSpans = TagHelperSpanVisitor.VisitRoot(syntaxTree);

            document.Items[typeof(TagHelperSpanInternal)] = tagHelperSpans;
        }

        return tagHelperSpans;
    }

    // Internal for testing
    internal static RazorLanguageKind GetLanguageKindCore(
        ImmutableArray<ClassifiedSpanInternal> classifiedSpans,
        ImmutableArray<TagHelperSpanInternal> tagHelperSpans,
        int hostDocumentIndex,
        int hostDocumentLength,
        bool rightAssociative)
    {
        var length = classifiedSpans.Length;
        for (var i = 0; i < length; i++)
        {
            var classifiedSpan = classifiedSpans[i];
            var span = classifiedSpan.Span;

            if (span.AbsoluteIndex <= hostDocumentIndex)
            {
                var end = span.AbsoluteIndex + span.Length;
                if (end >= hostDocumentIndex)
                {
                    if (end == hostDocumentIndex)
                    {
                        // We're at an edge.

                        if (classifiedSpan.SpanKind is SpanKindInternal.MetaCode or SpanKindInternal.Transition)
                        {
                            // If we're on an edge of a transition of some kind (MetaCode representing an open or closing piece of syntax such as <|,
                            // and Transition representing an explicit transition to/from razor syntax, such as @|), prefer to classify to the span
                            // to the right to better represent where the user clicks
                            continue;
                        }

                        // If we're right associative, then we don't want to use the classification that we're at the end
                        // of, if we're also at the start of the next one
                        if (rightAssociative)
                        {
                            if (i < classifiedSpans.Length - 1 && classifiedSpans[i + 1].Span.AbsoluteIndex == hostDocumentIndex)
                            {
                                // If we're at the start of the next span, then use that span
                                return GetLanguageFromClassifiedSpan(classifiedSpans[i + 1]);
                            }

                            // Otherwise, we did not find a match using right associativity, so check for tag helpers
                            break;
                        }
                    }

                    return GetLanguageFromClassifiedSpan(classifiedSpan);
                }
            }
        }

        foreach (var tagHelperSpan in tagHelperSpans)
        {
            var span = tagHelperSpan.Span;

            if (span.AbsoluteIndex <= hostDocumentIndex)
            {
                var end = span.AbsoluteIndex + span.Length;
                if (end >= hostDocumentIndex)
                {
                    if (end == hostDocumentIndex)
                    {
                        // We're at an edge. TagHelper spans never own their edge and aren't represented by marker spans
                        continue;
                    }

                    // Found intersection
                    return RazorLanguageKind.Html;
                }
            }
        }

        // Use the language of the last classified span if we're at the end
        // of the document.
        if (classifiedSpans.Length != 0 && hostDocumentIndex == hostDocumentLength)
        {
            var lastClassifiedSpan = classifiedSpans.Last();
            return GetLanguageFromClassifiedSpan(lastClassifiedSpan);
        }

        // Default to Razor
        return RazorLanguageKind.Razor;

        static RazorLanguageKind GetLanguageFromClassifiedSpan(ClassifiedSpanInternal classifiedSpan)
        {
            // Overlaps with request
            return classifiedSpan.SpanKind switch
            {
                SpanKindInternal.Markup => RazorLanguageKind.Html,
                SpanKindInternal.Code => RazorLanguageKind.CSharp,

                // Content type was non-C# or Html or we couldn't find a classified span overlapping the request position.
                // All other classified span kinds default back to Razor
                _ => RazorLanguageKind.Razor,
            };
        }
    }
}
