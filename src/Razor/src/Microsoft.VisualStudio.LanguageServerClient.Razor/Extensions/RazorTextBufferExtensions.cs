// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions
{
    internal static class RazorTextBufferExtensions
    {
        public static bool IsRazorLSPBuffer(this ITextBuffer textBuffer)
        {
            if (textBuffer is null)
            {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            var matchesContentType = textBuffer.ContentType.IsOfType(RazorLSPConstants.RazorLSPContentTypeName);
            return matchesContentType;
        }
    }
}
