// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial struct PooledArrayBuilder<T>
{
    public struct Enumerator(in PooledArrayBuilder<T> builder)
    {
        // Enumerate a copy of the original.
        private readonly PooledArrayBuilder<T> _builder = new(builder);
        private int _index = 0;
        private T _current = default!;

        public readonly T Current => _current;

        public bool MoveNext()
        {
            if (_index >= _builder.Count)
            {
                return false;
            }

            _current = _builder[_index];
            _index++;
            return true;
        }
    }
}
