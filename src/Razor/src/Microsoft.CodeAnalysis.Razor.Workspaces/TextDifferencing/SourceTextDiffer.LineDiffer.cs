// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.TextDifferencing;

internal partial class SourceTextDiffer
{
    private class LineDiffer : SourceTextDiffer
    {
        private readonly TextLineCollection _oldLines;
        private readonly TextLineCollection _newLines;

        private char[]? _oldLineBuffer;
        private char[]? _newLineBuffer;
        private char[]? _appendBuffer;

        public override int OldSourceLength { get; }
        public override int NewSourceLength { get; }

        public LineDiffer(SourceText oldText, SourceText newText)
            : base(oldText, newText)
        {
            _oldLines = oldText.Lines;
            _newLines = newText.Lines;

            OldSourceLength = _oldLines.Count;
            NewSourceLength = _newLines.Count;
        }

        public override bool SourceEqual(int oldSourceIndex, int newSourceIndex)
        {
            var oldLine = _oldLines[oldSourceIndex];
            var newLine = _newLines[newSourceIndex];

            var oldSpan = oldLine.SpanIncludingLineBreak;
            var newSpan = newLine.SpanIncludingLineBreak;

            if (oldSpan.Length != newSpan.Length)
            {
                return false;
            }

            var length = oldSpan.Length;

            // Simple case: Both lines are empty.
            if (length == 0)
            {
                return true;
            }

            var oldChars = GetBuffer(ref _oldLineBuffer, length);
            var newChars = GetBuffer(ref _newLineBuffer, length);

            OldText.CopyTo(oldSpan.Start, oldChars, 0, length);
            NewText.CopyTo(newSpan.Start, newChars, 0, length);

            for (var i = 0; i < length; i++)
            {
                if (oldChars[i] != newChars[i])
                {
                    return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char[] GetBuffer(ref char[]? array, int length)
        {
            if (array is not null && array.Length >= length)
            {
                return array;
            }

            array = new char[length];

            return array;
        }

        protected override int GetEditPosition(DiffEdit edit)
            => _oldLines[edit.Position].Start;

        protected override int AppendEdit(DiffEdit edit, StringBuilder builder)
        {
            if (edit.Kind == DiffEditKind.Insert)
            {
                Assumes.NotNull(edit.NewTextPosition);

                var newLine = _newLines[edit.NewTextPosition.Value];

                var newSpan = newLine.SpanIncludingLineBreak;
                if (newSpan.Length > 0)
                {
                    var buffer = GetBuffer(ref _appendBuffer, newSpan.Length);
                    NewText.CopyTo(newSpan.Start, buffer, 0, newSpan.Length);

                    builder.Append(buffer);
                }

                return _oldLines[edit.Position].Start;
            }
            else
            {
                return _oldLines[edit.Position].EndIncludingLineBreak;
            }
        }
    }
}
