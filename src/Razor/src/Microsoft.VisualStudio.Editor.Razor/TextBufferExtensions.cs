// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor;

namespace Microsoft.VisualStudio.Text;

internal static class TextBufferExtensions
{
    /// <summary>
    /// Indicates if a <paramref name="textBuffer"/> has the legacy Razor content type. This is represented by the projection based ASP.NET Core Razor editor.
    /// </summary>
    /// <param name="textBuffer">The text buffer to inspect</param>
    /// <returns><c>true</c> if the text buffers content type represents an ASP.NET Core projection based Razor editor content type.</returns>
    public static bool IsLegacyCoreRazorBuffer(this ITextBuffer textBuffer)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        return textBuffer.ContentType.IsOfType(RazorLanguage.CoreContentType) || textBuffer.ContentType.IsOfType(RazorConstants.LegacyCoreContentType);
    }

    /// <summary>
    /// Indicates if a <paramref name="textBuffer"/> has the LSP Razor content type. This is represented by the LSP based ASP.NET Core Razor editor.
    /// </summary>
    /// <param name="textBuffer">The text buffer to inspect</param>
    /// <returns><c>true</c> if the text buffers content type represents an ASP.NET Core LSP based Razor editor content type.</returns>
    public static bool IsRazorLSPBuffer(this ITextBuffer textBuffer)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        var matchesContentType = textBuffer.ContentType.IsOfType(RazorConstants.RazorLSPContentTypeName);
        return matchesContentType;
    }

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
