// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions
{
    internal static class RazorTextBufferExtensions
    {
        public static bool IsRazorLSPBuffer(this ITextBuffer textBuffer!!)
        {
            var matchesContentType = textBuffer.ContentType.IsOfType(RazorConstants.RazorLSPContentTypeName);
            return matchesContentType;
        }
    }
}
