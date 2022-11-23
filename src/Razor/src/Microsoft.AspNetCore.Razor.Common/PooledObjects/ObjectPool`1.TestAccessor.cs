// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

// Copied from https://github/dotnet/roslyn

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class ObjectPool<T>
    where T : class
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly ref struct TestAccessor
    {
        private readonly ObjectPool<T> _pool;

        internal TestAccessor(ObjectPool<T> pool)
        {
            _pool = pool;
        }

        public void Clear()
        {
            var size = Size;

            for (var i = 0; i < size; i++)
            {
                this[i] = null;
            }
        }

        public int Size
            => _pool._items.Length + 1;

        public ref T? this[int index]
            => ref index == 0
                ? ref _pool._firstItem
                : ref _pool._items[index - 1]._value;

        public int UsedSlotCount
        {
            get
            {
                var result = 0;

                if (_pool._firstItem is not null)
                {
                    result++;
                }

                foreach (var item in _pool._items)
                {
                    if (item._value is not null)
                    {
                        result++;
                    }
                }

                return result;
            }
        }
    }
}
