// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudio.LanguageServer.Protocol;

internal static partial class VsLspExtensions
{
    public static TextChange ToTextChange(this TextEdit textEdit, SourceText text)
    {
        ArgHelper.ThrowIfNull(textEdit);
        ArgHelper.ThrowIfNull(text);

        return new TextChange(text.GetTextSpan(textEdit.Range), textEdit.NewText);
    }
}
