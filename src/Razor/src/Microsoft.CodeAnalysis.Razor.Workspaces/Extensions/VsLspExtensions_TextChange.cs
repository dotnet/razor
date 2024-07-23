// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudio.LanguageServer.Protocol;

internal static partial class VsLspExtensions
{
    public static TextEdit ToTextEdit(this TextChange textChange, SourceText sourceText)
    {
        ArgHelper.ThrowIfNull(textChange);
        ArgHelper.ThrowIfNull(sourceText);

        Assumes.NotNull(textChange.NewText);

        return VsLspFactory.CreateTextEdit(textChange.Span.ToRange(sourceText), textChange.NewText);
    }

    public static RazorTextChange ToRazorTextChange(this TextChange textChange)
    {
        return new RazorTextChange()
        {
            Span = new RazorTextSpan()
            {
                Start = textChange.Span.Start,
                Length = textChange.Span.Length,
            },
            NewText = textChange.NewText
        };
    }
}
