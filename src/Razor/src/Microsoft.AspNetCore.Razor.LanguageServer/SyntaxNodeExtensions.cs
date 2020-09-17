// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        public static bool ContainsOnlyWhitespace(this SyntaxNode node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            var tokens = node.GetTokens();

            for (var i = 0; i < tokens.Count; i++)
            {
                var tokenKind = tokens[i].Kind;
                if (tokenKind != SyntaxKind.Whitespace && tokenKind != SyntaxKind.NewLine)
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

            var startLocation = source.Lines.GetLocationOurs(source, start);
            // var endLocation = source.Lines.GetLocation(end);
            var endLocation = source.Lines.GetLocationOurs(source, end);
            var startPosition = GetLinePosition(startLocation);
            var endPosition = GetLinePosition(endLocation);

            return new LinePositionSpan(startPosition, endPosition);

            static LinePosition GetLinePosition(SourceLocation location)
            {
                return new LinePosition(location.LineIndex, location.CharacterIndex);
            }
        }

        private static SourceLocation GetLocationOurs(this RazorSourceLineCollection lineCollection, RazorSourceDocument _document, int position)
        {
            var _lineStarts = GetLineStarts();

            if (position < 0 || position >= _document.Length)
            {
                throw new IndexOutOfRangeException(nameof(position));
            }

            var index = Array.BinarySearch<int>(_lineStarts, position);
            if (index >= 0)
            {
                // We have an exact match for the start of a line.
                Debug.Assert(_lineStarts[index] == position);

                return new SourceLocation(_document.GetFilePathForDisplay(), position, index, characterIndex: 0);
            }


            // Index is the complement of the line *after* the one we want, because BinarySearch tells
            // us where we'd put position *if* it were the start of a line.
            index = (~index) - 1;
            if (index == -1)
            {
                // There's no preceding line, so it's based on the start of the string
                return new SourceLocation(_document.GetFilePathForDisplay(), position, 0, position);
            }
            else
            {
                var characterIndex = position - _lineStarts[index];
                return new SourceLocation(_document.GetFilePathForDisplay(), position, index, characterIndex);
            }

            int[] GetLineStarts()
            {
                var starts = new List<int>();

                // We always consider a document to have at least a 0th line, even if it's empty.
                starts.Add(0);

                var unprocessedCR = false;

                // Length - 1 because we don't care if there was a linebreak as the last character.
                var length = _document.Length;
                for (var i = 0; i < length - 1; i++)
                {
                    var c = _document[i];
                    var isLineBreak = false;

                    switch (c)
                    {
                        case '\r':
                            unprocessedCR = true;
                            continue;

                        case '\n':
                            unprocessedCR = false;
                            isLineBreak = true;
                            break;

                        case '\u0085':
                        case '\u2028':
                        case '\u2029':
                            isLineBreak = true;
                            break;

                    }

                    if (unprocessedCR)
                    {
                        // If we get here it means that we had a CR followed by something other than an LF.
                        // Add the CR as a line break.
                        starts.Add(i);
                        unprocessedCR = false;
                    }

                    if (isLineBreak)
                    {
                        starts.Add(i + 1);
                    }
                }

                return starts.ToArray();
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
    }
}
