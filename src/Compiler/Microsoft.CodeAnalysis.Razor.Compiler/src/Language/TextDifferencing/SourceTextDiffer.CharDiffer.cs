// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

internal static partial class SourceTextDiffer
{
    private class CharDiffer : TextDiffer.CharDiffer
    {
        protected readonly SourceText OldText;
        protected readonly SourceText NewText;

        public CharDiffer(SourceText oldText, SourceText newText)
            : base(oldText.Length, newText.Length)
        {
            OldText = oldText ?? throw new ArgumentNullException(nameof(oldText));
            NewText = newText ?? throw new ArgumentNullException(nameof(newText));

            OldText.CopyTo(0, _oldBuffer.Array, 0, Math.Min(OldText.Length, BufferSize));
            NewText.CopyTo(0, _newBuffer.Array, 0, Math.Min(NewText.Length, BufferSize));
        }

        protected override int AppendEdit(DiffEdit edit, StringBuilder builder)
        {
            if (edit.Kind == DiffEditKind.Insert)
            {
                Assumed.NotNull(edit.NewTextPosition);
                var newTextPosition = edit.NewTextPosition.GetValueOrDefault();

                if (edit.Length > 1)
                {
                    var buffer = EnsureBuffer(ref _appendBuffer, edit.Length);
                    NewText.CopyTo(newTextPosition, buffer, 0, edit.Length);

                    builder.Append(buffer, 0, edit.Length);
                }
                else if (edit.Length == 1)
                {
                    builder.Append(NewText[newTextPosition]);
                }

                return edit.Position;
            }

            return edit.Position + edit.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void FillBuffer(ref Buffer buffer, bool isOldBuffer, int index)
        {
            var text = isOldBuffer ? OldText : NewText;

            // We slide our buffer so that index is in the middle. However, we have
            // have to be careful not extend past either the start or end of the SourceText.
            // Note that we always assume that we're filling the buffer with
            // BufferSize # of characters. If the SourceText is smaller than BufferSize,
            // this method shouldn't be called.

            Debug.Assert(text.Length >= BufferSize);

            var start = Math.Max(index - (BufferSize / 2), 0);

            if (start + BufferSize > text.Length)
            {
                start = text.Length - BufferSize;
            }

            text.CopyTo(start, buffer.Array, 0, BufferSize);
            buffer = new(buffer.Array, start, BufferSize);
        }
    }
}
