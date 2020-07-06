// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    public static class TextChangeExtensions
    {
        public static bool IsDelete(this ITextChange textChange)
        {
            return textChange.OldSpan.Length > 0 && textChange.NewText.Length == 0;
        }

        public static bool IsInsert(this ITextChange textChange)
        {
            return textChange.OldSpan.Length == 0 && textChange.NewText.Length > 0;
        }

        public static bool IsReplace(this ITextChange textChange)
        {
            return textChange.OldSpan.Length > 0 && textChange.NewText.Length > 0;
        }
    }
}
