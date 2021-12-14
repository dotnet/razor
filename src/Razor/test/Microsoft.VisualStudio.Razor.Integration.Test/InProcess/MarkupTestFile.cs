// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.Razor.Integration.Test.InProcess
{
    internal static class MarkupTestFile
    {
        internal static void GetPosition(string markupCode, out string code, out int caretPosition)
        {
            caretPosition = markupCode.IndexOf("$$");
            code = markupCode.Replace("$$", "");
        }
    }
}
