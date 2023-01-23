// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Buffers;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

internal abstract partial class IntArray
{
    private class SimpleArray : IntArray
    {
        private readonly int[] _array;
        private readonly bool _rented;

        public SimpleArray(int length)
            : base(length)
        {
            if (length > 0)
            {
                _array = ArrayPool<int>.Shared.Rent(length);
                _rented = true;
            }
            else
            {
                _array = Array.Empty<int>();
            }
        }

        public override void Dispose()
        {
            if (_rented)
            {
                ArrayPool<int>.Shared.Return(_array, clearArray: true);
            }
        }

        public override ref int this[int index]
            => ref _array[index];
    }
}
