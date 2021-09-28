// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions
{
    internal static class RangeExtensions
    {
        public static TextSpan AsTextSpan(this Range range, SourceText sourceText)
        {
            if (range is null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            var start = sourceText.Lines[range.Start.Line].Start + range.Start.Character;
            var end = sourceText.Lines[range.End.Line].Start + range.End.Character;
            return new TextSpan(start, end - start);
        }

        // Internal for testing only
        internal static readonly Range UndefinedRange = new()
        {
            Start = new Position(-1, -1),
            End = new Position(-1, -1)
        };

        public static bool IsUndefined(this Range range)
        {
            if (range is null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            return range == UndefinedRange;
        }
    }
}
