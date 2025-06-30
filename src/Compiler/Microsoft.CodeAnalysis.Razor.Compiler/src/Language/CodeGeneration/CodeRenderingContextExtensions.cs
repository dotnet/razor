// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

internal static class CodeRenderingContextExtensions
{
    public static IDisposable BuildLinePragma(
        this CodeRenderingContext context,
        SourceSpan? span,
        bool suppressLineDefaultAndHidden = false)
    {
        if (string.IsNullOrEmpty(span?.FilePath))
        {
            // Can't build a valid line pragma without a file path.
            return NullDisposable.Instance;
        }

        return new LinePragmaWriter(span.Value, context, 0, useEnhancedLinePragma: false, suppressLineDefaultAndHidden);
    }

    public static IDisposable BuildEnhancedLinePragma(
        this CodeRenderingContext context,
        SourceSpan? span,
        int characterOffset = 0,
        bool suppressLineDefaultAndHidden = false)
    {
        if (string.IsNullOrEmpty(span?.FilePath))
        {
            // Can't build a valid line pragma without a file path.
            return NullDisposable.Instance;
        }

        return new LinePragmaWriter(span.Value, context, characterOffset, useEnhancedLinePragma: true, suppressLineDefaultAndHidden);
    }

    private sealed class LinePragmaWriter : IDisposable
    {
        private readonly CodeRenderingContext _context;
        private readonly int _startIndent;
        private readonly int _startLineIndex;
        private readonly SourceSpan _span;
        private readonly bool _suppressLineDefaultAndHidden;

        public LinePragmaWriter(
            SourceSpan span,
            CodeRenderingContext context,
            int characterOffset,
            bool useEnhancedLinePragma = false,
            bool suppressLineDefaultAndHidden = false)
        {
            Debug.Assert(context.Options.DesignTime || useEnhancedLinePragma, "Runtime generation should only use enhanced line pragmas");

            _context = context;
            _suppressLineDefaultAndHidden = suppressLineDefaultAndHidden;
            _span = span;

            var writer = _context.CodeWriter;

            _startIndent = writer.CurrentIndent;
            writer.CurrentIndent = 0;

            var endsWithNewline = writer.LastChar is '\n';
            if (!endsWithNewline)
            {
                writer.WriteLine();
            }

            if (!_context.Options.SuppressNullabilityEnforcement)
            {
                writer.WriteLine("#nullable restore");
            }

            var ensurePathBackslashes = context.Options.RemapLinePragmaPathsOnWindows && PlatformInformation.IsWindows;
            if (useEnhancedLinePragma && _context.Options.UseEnhancedLinePragma)
            {
                writer.WriteEnhancedLineNumberDirective(span, characterOffset, ensurePathBackslashes);
            }
            else
            {
                writer.WriteLineNumberDirective(span, ensurePathBackslashes);
            }

            // Capture the line index after writing the #line directive.
            _startLineIndex = writer.Location.LineIndex;

            if (useEnhancedLinePragma)
            {
                // If the caller requested an enhanced line directive, but we fell back to regular ones, write out the extra padding that is required
                if (!_context.Options.UseEnhancedLinePragma)
                {
                    context.CodeWriter.WritePadding(0, span, context);
                    characterOffset = 0;
                }

                context.AddSourceMappingFor(span, characterOffset);
            }
        }

        public void Dispose()
        {
            var writer = _context.CodeWriter;

            // Need to add an additional line at the end IF there wasn't one already written.
            // This is needed to work with the C# editor's handling of #line ...
            var endsWithNewline = writer.LastChar is '\n';

            // Always write at least 1 empty line to potentially separate code from pragmas.
            writer.WriteLine();

            // Check if the previous empty line wasn't enough to separate code from pragmas.
            if (!endsWithNewline)
            {
                writer.WriteLine();
            }

            var lineCount = writer.Location.LineIndex - _startLineIndex;
            var linePragma = new LinePragma(
                _span.LineIndex,
                lineCount,
                _span.FilePath,
                _span.EndCharacterIndex,
                _span.EndCharacterIndex,
                _span.CharacterIndex);

            _context.AddLinePragma(linePragma);

            if (!_suppressLineDefaultAndHidden)
            {
                writer
                    .WriteLine("#line default")
                    .WriteLine("#line hidden");
            }

            if (!_context.Options.SuppressNullabilityEnforcement)
            {
                writer.WriteLine("#nullable disable");
            }

            writer.CurrentIndent = _startIndent;
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        private NullDisposable()
        {
        }

        public void Dispose()
        {
        }
    }
}
