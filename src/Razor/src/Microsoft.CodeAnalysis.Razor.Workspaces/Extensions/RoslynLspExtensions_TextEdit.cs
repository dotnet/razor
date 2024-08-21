// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using VsLspTextEdit = Microsoft.VisualStudio.LanguageServer.Protocol.TextEdit;

namespace Roslyn.LanguageServer.Protocol;

internal partial class RoslynLspExtensions
{
    public static VsLspTextEdit ToVsLspTextEdit(this TextEdit textEdit)
    {
        return new VsLspTextEdit()
        {
            Range = textEdit.Range.ToVsLspRange(),
            NewText = textEdit.NewText
        };
    }
}
