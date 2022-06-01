// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Text;
using VSRange = Microsoft.VisualStudio.LanguageServer.Protocol.Range;
using VSPosition = Microsoft.VisualStudio.LanguageServer.Protocol.Position;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class TextSpanExtensions
    {
        public static VSRange AsRange(this TextSpan span, SourceText sourceText)
        {
            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            sourceText.GetLinesAndOffsets(span, out var startLine, out var startChar, out var endLine, out var endChar);

            var range = new VSRange
            {
                Start = new VSPosition(startLine, startChar),
                End = new VSPosition(endLine, endChar)
            };

            return range;
        }
    }
}
