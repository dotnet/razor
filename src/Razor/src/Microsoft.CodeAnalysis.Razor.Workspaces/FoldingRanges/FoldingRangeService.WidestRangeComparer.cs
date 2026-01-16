// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Razor.FoldingRanges;

internal partial class FoldingRangeService
{
    private sealed class WidestRangeComparer : IComparer<FoldingRange>
    {
        public static WidestRangeComparer Instance = new();

        public int Compare(FoldingRange? x, FoldingRange? y)
        {
            if (x is null)
            {
                return y is null ? 0 : -1;
            }

            if (y is null)
            {
                return 1;
            }

            if (x.StartLine != y.StartLine)
            {
                return x.StartLine.CompareTo(y.StartLine);
            }

            if (x.EndLine != y.EndLine)
            {
                // NOTE: Comparing y to x here, because we want to sort the end descending, so we find the widest
                return y.EndLine.CompareTo(x.EndLine);
            }

            if (x.StartCharacter != y.StartCharacter)
            {
                return x.StartCharacter.GetValueOrDefault().CompareTo(y.StartCharacter.GetValueOrDefault());
            }

            // End char is descending too
            return y.EndCharacter.GetValueOrDefault().CompareTo(x.EndCharacter.GetValueOrDefault());
        }
    }
}
