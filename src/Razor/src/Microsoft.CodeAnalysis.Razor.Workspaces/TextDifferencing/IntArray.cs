// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

/// <summary>
///  This is a simple wrapper for either a single small int array, or
///  an array of int array pages.
/// </summary>
internal abstract partial class IntArray : IDisposable
{
    private const int PageSize = 1024 * 80 / sizeof(int);

    public readonly static IntArray Empty = new SimpleArray(0);

    public abstract ref int this[int index] { get; }
    public int Length { get; }

    protected IntArray(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Length = length;
    }

    public static IntArray Create(int length)
        => length > (PageSize / sizeof(int))
            ? new PagedArray(length)
            : new SimpleArray(length);

    public abstract void Dispose();
}
