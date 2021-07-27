// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal static class SyntaxNodeExtensions
    {
        public static bool ContainsOnlyWhitespace(this SyntaxNode node, bool includingNewLines = true)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var tokens = node.GetTokens();

            for (var i = 0; i < tokens.Count; i++)
            {
                var tokenKind = tokens[i].Kind;
                if (tokenKind != SyntaxKind.Whitespace && (tokenKind != SyntaxKind.NewLine || !includingNewLines))
                {
                    return false;
                }
            }

            // All tokens were either whitespace or newlines.
            return true;
        }

        public static LinePositionSpan GetLinePositionSpan(this SyntaxNode node, RazorSourceDocument source)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var start = node.Position;
            var end = node.EndPosition;

            Debug.Assert(start <= source.Length && end <= source.Length, "Node position exceeds source length.");

            if (start == source.Length && node.FullWidth == 0)
            {
                // Marker symbol at the end of the document.
                var location = node.GetSourceLocation(source);
                var position = GetLinePosition(location);
                return new LinePositionSpan(position, position);
            }

            var startLocation = source.Lines.GetLocation(start);
            var endLocation = source.Lines.GetLocation(end);
            var startPosition = GetLinePosition(startLocation);
            var endPosition = GetLinePosition(endLocation);

            return new LinePositionSpan(startPosition, endPosition);

            static LinePosition GetLinePosition(SourceLocation location)
            {
                return new LinePosition(location.LineIndex, location.CharacterIndex);
            }
        }

        public static Range GetRange(this SyntaxNode node, RazorSourceDocument source)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var lineSpan = node.GetLinePositionSpan(source);
            var range = new Range(
                new Position(lineSpan.Start.Line, lineSpan.Start.Character),
                new Position(lineSpan.End.Line, lineSpan.End.Character));

            return range;
        }

        public static Range? GetRangeWithoutWhitespace(this SyntaxNode node, RazorSourceDocument source)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var tokens = node.GetTokens();

            SyntaxToken firstToken = null;
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (!IsWhitespace(token))
                {
                    firstToken = token;
                    break;
                }
            }

            SyntaxToken lastToken = null;
            for (var i = tokens.Count - 1; i >= 0; i--)
            {
                var token = tokens[i];
                if (!IsWhitespace(token))
                {
                    lastToken = token;
                    break;
                }
            }

            if (firstToken is null && lastToken is null)
            {
                return null;
            }

            var startPositionSpan = GetLinePositionSpan(firstToken, source, node.SpanStart);
            var endPositionSpan = GetLinePositionSpan(lastToken, source, node.Span.Start);

            var range = new Range(
                new Position(startPositionSpan.Start.Line, startPositionSpan.Start.Character),
                new Position(endPositionSpan.End.Line, endPositionSpan.End.Character));

            return range;

            // This is needed because SyntaxToken positions taken from GetTokens
            // are relative to their parent node and not to the document.
            static LinePositionSpan GetLinePositionSpan(SyntaxNode node, RazorSourceDocument source, int parentStart)
            {
                if (node is null)
                {
                    throw new ArgumentNullException(nameof(node));
                }

                if (source is null)
                {
                    throw new ArgumentNullException(nameof(source));
                }

                var start = node.Position + parentStart;
                var end = node.EndPosition + parentStart;

                if (start == source.Length && node.FullWidth == 0)
                {
                    // Marker symbol at the end of the document.
                    var location = node.GetSourceLocation(source);
                    var position = GetLinePosition(location);
                    return new LinePositionSpan(position, position);
                }

                var startLocation = source.Lines.GetLocation(start);
                var endLocation = source.Lines.GetLocation(end);
                var startPosition = GetLinePosition(startLocation);
                var endPosition = GetLinePosition(endLocation);

                return new LinePositionSpan(startPosition, endPosition);

                static LinePosition GetLinePosition(SourceLocation location)
                {
                    return new LinePosition(location.LineIndex, location.CharacterIndex);
                }
            }
        }

        public static int GetLeadingWhitespaceLength(this SyntaxNode node)
        {
            var tokens = node.GetTokens();
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (!IsWhitespace(token))
                {
                    return i;
                }
            }

            return 0;
        }

        public static int GetTrailingWhitespaceLength(this SyntaxNode node)
        {
            var tokens = node.GetTokens();
            for (var i = 0;  i < tokens.Count; i++)
            {
                var token = tokens[(tokens.Count - 1) - i];
                if (!IsWhitespace(token))
                {
                    return i;
                }
            }

            return 0;
        }

        private static bool IsWhitespace(SyntaxToken token)
        {
            return token.Kind == SyntaxKind.Whitespace || token.Kind == SyntaxKind.NewLine;
        }
    }
}
