// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.RazorLS;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

#nullable enable

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class PositionExtensions
    {
        public static bool TryGetAbsoluteIndex(this Position position, SourceText sourceText, out int absoluteIndex, ILogger? logger = null)
        {
            if (position is null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            var linePosition = new LinePosition(position.Line, position.Character);
            if (linePosition.Line >= sourceText.Lines.Count)
            {
                var errorMessage = Resources.FormatPositionIndex_Outside_Range(
                    position.Line,
                    nameof(sourceText),
                    sourceText.Lines.Count);
                logger?.LogError(errorMessage);
                absoluteIndex = -1;
                return false;
            }
            var index = sourceText.Lines.GetPosition(linePosition);
            absoluteIndex = index;
            return true;
        }

        public static int CompareTo(this Position position, Position other)
        {
            if (position is null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            var result = position.Line.CompareTo(other.Line);
            return result != 0 ? result : position.Character.CompareTo(other.Character);
        }

        public static bool IsValid(this Position position, SourceText sourceText)
        {
            if (position is null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            return position.Line >= 0 &&
                position.Character >= 0 &&
                position.Line < sourceText.Lines.Count &&
                sourceText.Lines[position.Line].Start + position.Character <= sourceText.Length;
        }
    }
}
