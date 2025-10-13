// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.Language;

internal static partial class RazorCodeDocumentExtensions
{
    /// <summary>
    /// Adjusts the position if it's on a component end tag to use the corresponding start tag position.
    /// This ensures that hover, go to definition, and find all references work consistently for both
    /// start and end tags, since only start tags have source mappings.
    /// </summary>
    /// <param name="codeDocument">The code document.</param>
    /// <param name="hostDocumentIndex">The position in the host document.</param>
    /// <returns>
    /// The adjusted position if on a component end tag's name, otherwise the original position.
    /// </returns>
    public static int AdjustPositionForComponentEndTag(this RazorCodeDocument codeDocument, int hostDocumentIndex)
    {
        var root = codeDocument.GetRequiredSyntaxRoot();
        var owner = root.FindInnermostNode(hostDocumentIndex, includeWhitespace: false);
        if (owner is null)
        {
            return hostDocumentIndex;
        }

        // Check if we're on a component end tag
        if (owner.FirstAncestorOrSelf<MarkupTagHelperEndTagSyntax>() is { } endTag)
        {
            // Check if the position is within the tag name
            if (endTag.Name.Span.IntersectsWith(hostDocumentIndex))
            {
                // Get the corresponding start tag
                var startTag = endTag.GetStartTag();
                if (startTag is MarkupTagHelperStartTagSyntax tagHelperStartTag)
                {
                    // Return the position at the start of the start tag's name
                    // This ensures the position maps to where C# code is generated
                    return tagHelperStartTag.Name.SpanStart;
                }
            }
        }

        return hostDocumentIndex;
    }
}
