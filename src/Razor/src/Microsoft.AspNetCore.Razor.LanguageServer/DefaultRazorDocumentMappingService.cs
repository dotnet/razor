// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultRazorDocumentMappingService : RazorDocumentMappingService
    {
        public override bool TryMapFromProjectedDocumentRange(RazorCodeDocument codeDocument, Range projectedRange, out Range originalRange)
        {
            originalRange = default;
            if (codeDocument.IsUnsupported())
            {
                // All mapping requests on unsupported documents return undefined ranges. This is equivalent to what pre-VSCode Razor was capable of.
                return false;
            }

            var csharpSourceText = SourceText.From(codeDocument.GetCSharpDocument().GeneratedCode);
            var range = projectedRange;
            var startPosition = range.Start;
            var lineStartPosition = new LinePosition((int)startPosition.Line, (int)startPosition.Character);
            var startIndex = csharpSourceText.Lines.GetPosition(lineStartPosition);
            if (!TryMapFromProjectedDocumentPosition(codeDocument, startIndex, out var hostDocumentStart, out var _))
            {
                return false;
            }

            var endPosition = range.End;
            var lineEndPosition = new LinePosition((int)endPosition.Line, (int)endPosition.Character);
            var endIndex = csharpSourceText.Lines.GetPosition(lineEndPosition);
            if (!TryMapFromProjectedDocumentPosition(codeDocument, endIndex, out var hostDocumentEnd, out var _))
            {
                return false;
            }

            originalRange = new Range(
                hostDocumentStart,
                hostDocumentEnd);

            return true;
        }

        public override bool TryMapToProjectedDocumentRange(RazorCodeDocument codeDocument, Range originalRange, out Range projectedRange)
        {
            projectedRange = default;
            var source = codeDocument.Source;
            var charBuffer = new char[source.Length];
            source.CopyTo(0, charBuffer, 0, source.Length);
            var sourceText = SourceText.From(new string(charBuffer));
            var range = originalRange;

            var startPosition = range.Start;
            var lineStartPosition = new LinePosition((int)startPosition.Line, (int)startPosition.Character);
            var startIndex = sourceText.Lines.GetPosition(lineStartPosition);
            if (!TryMapToProjectedDocumentPosition(codeDocument, startIndex, out var projectedStart, out var _))
            {
                return false;
            }

            var endPosition = range.End;
            var lineEndPosition = new LinePosition((int)endPosition.Line, (int)endPosition.Character);
            var endIndex = sourceText.Lines.GetPosition(lineEndPosition);
            if (!TryMapToProjectedDocumentPosition(codeDocument, endIndex, out var projectedEnd, out var _))
            {
                return false;
            }

            projectedRange = new Range(
                projectedStart,
                projectedEnd);

            return true;
        }

        public override bool TryMapFromProjectedDocumentPosition(RazorCodeDocument codeDocument, int csharpAbsoluteIndex, out Position originalPosition, out int originalIndex)
        {
            var csharpDoc = codeDocument.GetCSharpDocument();
            foreach (var mapping in csharpDoc.SourceMappings)
            {
                var generatedSpan = mapping.GeneratedSpan;
                var generatedAbsoluteIndex = generatedSpan.AbsoluteIndex;
                if (generatedAbsoluteIndex <= csharpAbsoluteIndex)
                {
                    // Treat the mapping as owning the edge at its end (hence <= originalSpan.Length),
                    // otherwise we wouldn't handle the cursor being right after the final C# char
                    var distanceIntoGeneratedSpan = csharpAbsoluteIndex - generatedAbsoluteIndex;
                    if (distanceIntoGeneratedSpan <= generatedSpan.Length)
                    {
                        // Found the generated span that contains the csharp absolute index

                        originalIndex = mapping.OriginalSpan.AbsoluteIndex + distanceIntoGeneratedSpan;
                        var originalLocation = codeDocument.Source.Lines.GetLocation(originalIndex);
                        originalPosition = new Position(originalLocation.LineIndex, originalLocation.CharacterIndex);
                        return true;
                    }
                }
            }

            originalPosition = default;
            originalIndex = default;
            return false;
        }

        public override bool TryMapToProjectedDocumentPosition(RazorCodeDocument codeDocument, int absoluteIndex, out Position projectedPosition, out int projectedIndex)
        {
            var csharpDoc = codeDocument.GetCSharpDocument();
            foreach (var mapping in csharpDoc.SourceMappings)
            {
                var originalSpan = mapping.OriginalSpan;
                var originalAbsoluteIndex = originalSpan.AbsoluteIndex;
                if (originalAbsoluteIndex <= absoluteIndex)
                {
                    // Treat the mapping as owning the edge at its end (hence <= originalSpan.Length),
                    // otherwise we wouldn't handle the cursor being right after the final C# char
                    var distanceIntoOriginalSpan = absoluteIndex - originalAbsoluteIndex;
                    if (distanceIntoOriginalSpan <= originalSpan.Length)
                    {
                        var generatedSource = SourceText.From(csharpDoc.GeneratedCode);
                        projectedIndex = mapping.GeneratedSpan.AbsoluteIndex + distanceIntoOriginalSpan;
                        var generatedLinePosition = generatedSource.Lines.GetLinePosition(projectedIndex);
                        projectedPosition = new Position(generatedLinePosition.Line, generatedLinePosition.Character);
                        return true;
                    }
                }
            }

            projectedPosition = default;
            projectedIndex = default;
            return false;
        }
    }
}
