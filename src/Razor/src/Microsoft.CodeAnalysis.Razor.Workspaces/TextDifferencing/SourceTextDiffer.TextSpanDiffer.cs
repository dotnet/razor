// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

internal partial class SourceTextDiffer
{
    private abstract class TextSpanDiffer : SourceTextDiffer
    {
        private readonly ImmutableArray<TextSpan> _oldLines = [];
        private readonly ImmutableArray<TextSpan> _newLines = [];

        private char[] _oldLineBuffer;
        private char[] _newLineBuffer;
        private char[] _appendBuffer;

        protected override int OldSourceLength { get; }
        protected override int NewSourceLength { get; }

        public TextSpanDiffer(SourceText oldText, SourceText newText)
            : base(oldText, newText)
        {
            _oldLineBuffer = RentArray(1024);
            _newLineBuffer = RentArray(1024);
            _appendBuffer = RentArray(1024);

            if (oldText.Length > 0)
            {
                _oldLines = Tokenize(oldText);
            }

            if (newText.Length > 0)
            {
                _newLines = Tokenize(newText);
            }

            OldSourceLength = _oldLines.Length;
            NewSourceLength = _newLines.Length;
        }

        protected abstract ImmutableArray<TextSpan> Tokenize(SourceText text);

        public override void Dispose()
        {
            ReturnArray(_oldLineBuffer);
            ReturnArray(_newLineBuffer);
            ReturnArray(_appendBuffer);
        }

        protected override bool SourceEqual(int oldSourceIndex, int newSourceIndex)
        {
            var oldLine = _oldLines[oldSourceIndex];
            var newLine = _newLines[newSourceIndex];

            if (oldLine.Length != newLine.Length)
            {
                return false;
            }

            var length = oldLine.Length;

            // Simple case: Both lines are empty.
            if (length == 0)
            {
                return true;
            }

            // Copy the text into char arrays for comparison. Note: To avoid allocation,
            // we try to reuse the same char buffers and only grow them when a longer
            // line is encountered.
            var oldChars = EnsureBuffer(ref _oldLineBuffer, length);
            var newChars = EnsureBuffer(ref _newLineBuffer, length);

            OldText.CopyTo(oldLine.Start, oldChars, 0, length);
            NewText.CopyTo(newLine.Start, newChars, 0, length);

            for (var i = 0; i < length; i++)
            {
                if (oldChars[i] != newChars[i])
                {
                    return false;
                }
            }

            return true;
        }

        protected override int GetEditPosition(DiffEdit edit)
            => _oldLines[edit.Position].Start;

        protected override int AppendEdit(DiffEdit edit, StringBuilder builder)
        {
            if (edit.Kind == DiffEditKind.Insert)
            {
                Assumes.NotNull(edit.NewTextPosition);
                var newTextPosition = edit.NewTextPosition.GetValueOrDefault();

                for (var i = 0; i < edit.Length; i++)
                {
                    var newLine = _newLines[newTextPosition + i];

                    if (newLine.Length > 0)
                    {
                        var buffer = EnsureBuffer(ref _appendBuffer, newLine.Length);
                        NewText.CopyTo(newLine.Start, buffer, 0, newLine.Length);

                        builder.Append(buffer, 0, newLine.Length);
                    }
                }

                return _oldLines[edit.Position].Start;
            }

            return _oldLines[edit.Position + edit.Length - 1].End;
        }
    }
}
