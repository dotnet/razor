// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Text;
using RoslynTextChange = Microsoft.CodeAnalysis.Text.TextChange;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
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

        public static IReadOnlyList<ITextChange> ToVisualStudioTextChangeList(this IReadOnlyList<RoslynTextChange> roslynTextChanges)
        {
            if (roslynTextChanges == null)
            {
                return null;
            }

            if (roslynTextChanges.Count == 0)
            {
                return Array.Empty<ITextChange>();
            }

            var textChanges = new List<ITextChange>(roslynTextChanges.Count);
            for (var i = 0; i < roslynTextChanges.Count; i++)
            {
                var roslynTextChange = roslynTextChanges[i];
                textChanges.Add(new SimpleTextChange(roslynTextChange.Span.Start, roslynTextChange.Span.Length, roslynTextChange.NewText));
            }

            return textChanges;
        }
    }
}
