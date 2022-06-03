// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.RazorLS;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharpPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class PositionExtensions
    {
        public static Position AsVSPosition(this OmniSharpPosition position)
        {
            return new Position(position.Line, position.Character);
        }

        public static OmniSharpPosition AsOSharpPosition(this Position position)
        {
            return new OmniSharpPosition(position.Line, position.Character);
        }

        public static bool TryGetAbsoluteIndex(this Position position, SourceText sourceText, ILogger logger, out int absoluteIndex)
        {
            if (position is null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            return TryGetAbsoluteIndex(position.Character, position.Line, sourceText, logger, out absoluteIndex);
        }

        public static bool TryGetAbsoluteIndex(this OmniSharpPosition position, SourceText sourceText, ILogger logger, out int absoluteIndex)
        {
            if (position is null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            return TryGetAbsoluteIndex(position.Character, position.Line, sourceText, logger, out absoluteIndex);
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

        public static int CompareTo(this OmniSharpPosition position, OmniSharpPosition other)
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

        private static bool TryGetAbsoluteIndex(int character, int line, SourceText sourceText, ILogger logger, out int absoluteIndex)
        {
            var linePosition = new LinePosition(line, character);
            if (linePosition.Line >= sourceText.Lines.Count)
            {
                var errorMessage = Resources.FormatPositionIndex_Outside_Range(
                    line,
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
    }
}
