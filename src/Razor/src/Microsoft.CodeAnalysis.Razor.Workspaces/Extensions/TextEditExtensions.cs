// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal static class TextEditExtensions
{
    public static TextChange ToTextChange(this TextEdit textEdit, SourceText text)
    {
        ArgHelper.ThrowIfNull(textEdit);
        ArgHelper.ThrowIfNull(text);

        return new TextChange(text.GetTextSpan(textEdit.Range), textEdit.NewText);
    }
}
