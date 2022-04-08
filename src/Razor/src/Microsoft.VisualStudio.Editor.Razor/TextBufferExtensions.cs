// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor;

namespace Microsoft.VisualStudio.Text
{
    internal static class TextBufferExtensions
    {
        public static bool IsLegacyCoreRazorBuffer(this ITextBuffer textBuffer!!)
        {
            return textBuffer.ContentType.IsOfType(RazorLanguage.CoreContentType) || textBuffer.ContentType.IsOfType(RazorConstants.LegacyCoreContentType);
        }

        public static bool IsRazorLSPBuffer(this ITextBuffer textBuffer!!)
        {
            var matchesContentType = textBuffer.ContentType.IsOfType(RazorConstants.RazorLSPContentTypeName);
            return matchesContentType;
        }
    }
}
