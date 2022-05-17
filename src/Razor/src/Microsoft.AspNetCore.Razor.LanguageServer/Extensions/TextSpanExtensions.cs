// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Text;
using VSRange = Microsoft.VisualStudio.LanguageServer.Protocol.Range;
using VSPosition = Microsoft.VisualStudio.LanguageServer.Protocol.Position;
using OmniRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using OmniPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class TextSpanExtensions
    {
        public static VSRange AsVSRange(this TextSpan span, SourceText sourceText)
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

        public static OmniRange AsRange(this TextSpan span, SourceText sourceText)
        {
            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            sourceText.GetLinesAndOffsets(span, out var startLine, out var startChar, out var endLine, out var endChar);

            var range = new OmniRange
            {
                Start = new OmniPosition(startLine, startChar),
                End = new OmniPosition(endLine, endChar)
            };

            return range;
        }
    }
}
