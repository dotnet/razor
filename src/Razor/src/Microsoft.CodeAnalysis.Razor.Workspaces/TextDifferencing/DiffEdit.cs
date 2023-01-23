// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.TextDifferencing;

internal readonly struct DiffEdit
{
    public DiffEditKind Kind { get; }
    public int Position { get; }
    public int? NewTextPosition { get; }

    private DiffEdit(DiffEditKind kind, int position, int? newTextPosition)
    {
        Kind = kind;
        Position = position;
        NewTextPosition = newTextPosition;
    }

    public static DiffEdit Insert(int position, int newTextPosition)
        => new(DiffEditKind.Insert, position, newTextPosition);

    public static DiffEdit Delete(int position)
        => new(DiffEditKind.Delete, position, newTextPosition: null);
}
