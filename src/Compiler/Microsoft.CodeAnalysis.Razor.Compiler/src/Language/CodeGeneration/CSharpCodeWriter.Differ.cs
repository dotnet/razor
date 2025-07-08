// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis.Text;

using static Microsoft.CodeAnalysis.Razor.TextDifferencing.TextDiffer;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public partial class CSharpCodeWriter
{
    private class Differ : CharDiffer
    {
        private readonly SourceText _previousCSharpSourceText;
        private readonly Reader _newText;

        private Differ(SourceText previousCSharpSourceText, Reader newText)
            : base(previousCSharpSourceText.Length, newText.Length)
        {
            _previousCSharpSourceText = previousCSharpSourceText;
            _newText = newText;

            _previousCSharpSourceText.CopyTo(0, _oldBuffer.Array, 0, Math.Min(previousCSharpSourceText.Length, BufferSize));

            _newText.SetPosition(0);
            _newText.Read(_newBuffer.Array, 0, Math.Min(newText.Length, BufferSize));
        }

        public static ImmutableArray<TextChange> GetMinimalTextChanges(SourceText previousCSharpSourceText, Reader newText)
        {
            using var differ = new Differ(previousCSharpSourceText, newText);

            var changes = differ.GetMinimalTextChanges();

#if DEBUG
            newText.SetPosition(0);
            Debug.Assert(previousCSharpSourceText.WithChanges(changes).ToString() == newText.ReadToEnd(), "Incorrect minimal changes");
#endif

            return changes;
        }

        protected override int AppendEdit(DiffEdit edit, StringBuilder builder)
        {
            if (edit.Kind == DiffEditKind.Insert)
            {
                Assumed.NotNull(edit.NewTextPosition);
                var newTextPosition = edit.NewTextPosition.GetValueOrDefault();

                if (edit.Length > 0)
                {
                    var buffer = EnsureBuffer(ref _appendBuffer, edit.Length);

                    _newText.SetPosition(newTextPosition);
                    _newText.Read(buffer, 0, edit.Length);

                    builder.Append(buffer, 0, edit.Length);
                }

                return edit.Position;
            }

            return edit.Position + edit.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void FillBuffer(ref Buffer buffer, bool isOldBuffer, int index)
        {
            // We slide our buffer so that index is in the middle. However, we have
            // have to be careful not extend past either the start or end of the SourceText.
            // Note that we always assume that we're filling the buffer with
            // BufferSize # of characters. If the SourceText is smaller than BufferSize,
            // this method shouldn't be called.
            if (isOldBuffer)
            {
                Debug.Assert(_previousCSharpSourceText.Length >= BufferSize);

                var start = Math.Max(index - (BufferSize / 2), 0);

                if (start + BufferSize > _previousCSharpSourceText.Length)
                {
                    start = _previousCSharpSourceText.Length - BufferSize;
                }

                _previousCSharpSourceText.CopyTo(start, buffer.Array, 0, BufferSize);
                buffer = new(buffer.Array, start, BufferSize);
            }
            else
            {
                Debug.Assert(_newText.Length >= BufferSize);

                var start = Math.Max(index - (BufferSize / 2), 0);

                if (start + BufferSize > _newText.Length)
                {
                    start = _newText.Length - BufferSize;
                }

                _newText.SetPosition(start);
                _newText.Read(buffer.Array, 0, BufferSize);

                buffer = new(buffer.Array, start, BufferSize);
            }
        }
    }
}
