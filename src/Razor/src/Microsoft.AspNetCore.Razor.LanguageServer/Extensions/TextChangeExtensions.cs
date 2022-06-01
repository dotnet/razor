// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.CodeAnalysis.Text;
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class TextChangeExtensions
    {
        public static VS.TextEdit AsVSTextEdit(this TextChange textChange, SourceText sourceText)
        {
            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            var range = textChange.Span.AsRange(sourceText);

            return new VS.TextEdit()
            {
                NewText = textChange.NewText,
                Range = range
            };
        }
    }
}
