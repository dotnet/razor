// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

internal partial class TextDiffer
{
    internal abstract class CharDiffer : TextDiffer
    {
        internal readonly struct Buffer
        {
            public readonly char[] Array;
            public readonly int Start;
            public readonly int Length;

            public Buffer(char[] array, int start, int length)
                => (Array, Start, Length) = (array, start, length);

            public void Deconstruct(out char[] array, out int start, out int length)
                => (array, start, length) = (Array, Start, Length);

            public char this[int index]
                => Array[index - Start];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(int index)
                => index >= Start && index < Start + Length;
        }

        protected const int BufferSize = 1024 * 16;

        protected override int OldSourceLength { get; }
        protected override int NewSourceLength { get; }

        protected char[] _appendBuffer;
        protected Buffer _oldBuffer;
        protected Buffer _newBuffer;

        public CharDiffer(int oldSourceLength, int newSourceLength)
        {
            _appendBuffer = RentArray(BufferSize);

            _oldBuffer = new(RentArray(BufferSize), 0, BufferSize);
            _newBuffer = new(RentArray(BufferSize), 0, BufferSize);

            OldSourceLength = oldSourceLength;
            NewSourceLength = newSourceLength;
        }

        public override void Dispose()
        {
            ReturnArray(_appendBuffer);
            ReturnArray(_oldBuffer.Array);
            ReturnArray(_newBuffer.Array);
        }

        protected abstract void FillBuffer(ref Buffer buffer, bool isOldBuffer, int index);

        protected override bool SourceEqual(int oldSourceIndex, int newSourceIndex)
        {
            ref var oldBuffer = ref _oldBuffer;
            ref var newBuffer = ref _newBuffer;

            if (!oldBuffer.Contains(oldSourceIndex))
            {
                FillBuffer(ref oldBuffer, isOldBuffer: true, oldSourceIndex);
            }

            if (!newBuffer.Contains(newSourceIndex))
            {
                FillBuffer(ref newBuffer, isOldBuffer: false, newSourceIndex);
            }

            return oldBuffer[oldSourceIndex] == newBuffer[newSourceIndex];
        }

        protected override int GetEditPosition(DiffEdit edit)
            => edit.Position;
    }
}
