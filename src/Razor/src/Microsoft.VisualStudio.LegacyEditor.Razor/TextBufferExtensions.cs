// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.LegacyEditor.Razor.Parsing;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LegacyEditor.Razor;

internal static class TextBufferExtensions
{
    public static bool TryGetCodeDocument(this ITextBuffer textBuffer, [NotNullWhen(true)] out RazorCodeDocument? codeDocument)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        if (textBuffer.Properties.TryGetProperty(typeof(IVisualStudioRazorParser), out IVisualStudioRazorParser parser) &&
            parser.CodeDocument is not null)
        {
            codeDocument = parser.CodeDocument;
            return true;
        }

        codeDocument = null;
        return false;
    }
}
