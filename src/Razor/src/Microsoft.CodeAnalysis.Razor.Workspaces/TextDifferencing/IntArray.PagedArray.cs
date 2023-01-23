// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Buffers;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

internal abstract partial class IntArray
{
    private class PagedArray : IntArray
    {
        private readonly int[][] _pages;

        public PagedArray(int length)
            : base(length)
        {
            var fullSizePageCount = length / PageSize;
            var finalPageSize = length % PageSize;
            var arraySize = fullSizePageCount;

            // If length is not evenly divisible by PageSize,
            // we must increase the number of pages.
            if (finalPageSize > 0)
            {
                arraySize++;
            }

            _pages = new int[arraySize][];

            // Rent arrays for the pages that are of length, PageSize.
            for (var i = 0; i < fullSizePageCount; i++)
            {
                _pages[i]= ArrayPool<int>.Shared.Rent(PageSize);
            }

            if (finalPageSize > 0)
            {
                // Rent an array for the final page's length.
                _pages[^1] = ArrayPool<int>.Shared.Rent(finalPageSize);
            }
        }

        public override void Dispose()
        {
            foreach (var page in _pages)
            {
                // Return all of the arrays to the pool. Note that we clear each
                // to ensure that they are empty on reuse.
                ArrayPool<int>.Shared.Return(page, clearArray: true);
            }
        }

        public override ref int this[int index]
            => ref _pages[index / PageSize][index % PageSize];
    }
}
