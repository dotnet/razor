// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

//
// This class implements the linear space variation of the difference algorithm described in
// "An O(ND) Difference Algorithm and its Variations" by Eugene W. Myers
//
// Note: Some variable names in this class are not be fully compliant with the C# naming guidelines
// in the interest of using the same terminology discussed in the paper for ease of understanding.
//
internal abstract partial class TextDiffer : IDisposable
{
    protected abstract int OldSourceLength { get; }
    protected abstract int NewSourceLength { get; }

    private int _oldSourceOffset;
    private int _newSourceOffset;

    protected abstract bool SourceEqual(int oldSourceIndex, int newSourceIndex);

    protected List<DiffEdit> ComputeDiff()
    {
        var edits = new List<DiffEdit>(capacity: 4);
        var builder = new DiffEditBuilder(edits);

        var lowA = 0;
        var highA = OldSourceLength;
        var lowB = 0;
        var highB = NewSourceLength;

        // Determine the extent of the changes in both texts, as this will allow us
        // to limit the amount of memory needed in the vf/vr arrays. By doing this though,
        // we will need to adjust SourceEqual requests to use an appropriate offset.
        FindChangeExtent(ref lowA, ref highA, ref lowB, ref highB);

        _oldSourceOffset = lowA;
        _newSourceOffset = lowB;

        var oldSourceLength = highA - lowA;
        var newSourceLength = highB - lowB;

        // Initialize the vectors to use for forward and reverse searches.
        var max = newSourceLength + oldSourceLength;
        using var vf = new IntArray((2 * max) + 1);
        using var vr = new IntArray((2 * max) + 1);

        ComputeDiffRecursive(builder, 0, oldSourceLength, 0, newSourceLength, vf, vr);

        // Update the resultant edits with the appropriate offsets
        for (var i = 0; i < edits.Count; i++)
        {
            edits[i] = edits[i].Offset(_oldSourceOffset, _newSourceOffset);
        }

        return edits;
    }

    private void ComputeDiffRecursive(DiffEditBuilder edits, int lowA, int highA, int lowB, int highB, IntArray vf, IntArray vr)
    {
        FindChangeExtent(ref lowA, ref highA, ref lowB, ref highB);

        if (lowA == highA)
        {
            // Base case 1: We've reached the end of original text. Insert whatever is remaining in the new text.
            while (lowB < highB)
            {
                edits.AddInsert(lowA, lowB);
                lowB++;
            }
        }
        else if (lowB == highB)
        {
            // Base case 2: We've reached the end of new text. Delete whatever is remaining in the original text.
            while (lowA < highA)
            {
                edits.AddDelete(lowA);
                lowA++;
            }
        }
        else
        {
            // Find the midpoint of the optimal path.
            var (middleX, middleY) = FindMiddleSnake(lowA, highA, lowB, highB, vf, vr);

            // Recursively find the midpoint of the left half.
            ComputeDiffRecursive(edits, lowA, middleX, lowB, middleY, vf, vr);

            // Recursively find the midpoint of the right half.
            ComputeDiffRecursive(edits, middleX, highA, middleY, highB, vf, vr);
        }
    }

    private void FindChangeExtent(ref int lowA, ref int highA, ref int lowB, ref int highB)
    {
        while (lowA < highA && lowB < highB && SourceEqualUsingOffset(lowA, lowB))
        {
            // Skip equal text at the start.
            lowA++;
            lowB++;
        }

        while (lowA < highA && lowB < highB && SourceEqualUsingOffset(highA - 1, highB - 1))
        {
            // Skip equal text at the end.
            highA--;
            highB--;
        }
    }

    private (int, int) FindMiddleSnake(int lowA, int highA, int lowB, int highB, IntArray vf, IntArray vr)
    {
        var n = highA - lowA;
        var m = highB - lowB;
        var delta = n - m;
        var deltaIsEven = delta % 2 == 0;

        var max = n + m;

        // Compute the k-line to start the forward and reverse searches.
        var forwardK = lowA - lowB;
        var reverseK = highA - highB;

        // The paper uses negative indexes but we can't do that here. So we'll add an offset.
        var forwardOffset = max - forwardK;
        var reverseOffset = max - reverseK;

        // Initialize the vector
        vf[forwardOffset + forwardK + 1] = lowA;
        vr[reverseOffset + reverseK - 1] = highA;

        var maxD = Math.Ceiling((double)(m + n) / 2);
        for (var d = 0; d <= maxD; d++) // For D ← 0 to ceil((M + N)/2) Do
        {
            // Run the algorithm in forward direction.
            for (var k = forwardK - d; k <= forwardK + d; k += 2) // For k ← −D to D in steps of 2 Do
            {
                // Find the end of the furthest reaching forward D-path in diagonal k.
                int x;
                if (k == forwardK - d ||
                    (k != forwardK + d && vf[forwardOffset + k - 1] < vf[forwardOffset + k + 1]))
                {
                    // Down
                    x = vf[forwardOffset + k + 1];
                }
                else
                {
                    // Right
                    x = vf[forwardOffset + k - 1] + 1;
                }

                var y = x - k;

                // Traverse diagonal if possible.
                while (x < highA && y < highB && SourceEqualUsingOffset(x, y))
                {
                    x++;
                    y++;
                }

                vf[forwardOffset + k] = x;
                if (deltaIsEven)
                {
                    // Can't have overlap here.
                }
                else if (k > reverseK - d && k < reverseK + d) // If ∆ is odd and k ∈ [∆ − (D − 1) , ∆ + (D − 1)] Then
                {
                    if (vr[reverseOffset + k] <= vf[forwardOffset + k]) // If the path overlaps the furthest reaching reverse (D − 1)-path in diagonal k Then
                    {
                        // The last snake of the forward path is the middle snake.
                        x = vf[forwardOffset + k];
                        y = x - k;
                        return (x, y);
                    }
                }
            }

            // Run the algorithm in reverse direction.
            for (var k = reverseK - d; k <= reverseK + d; k += 2) // For k ← −D to D in steps of 2 Do
            {
                // Find the end of the furthest reaching reverse D-path in diagonal k+∆.
                int x;
                if (k == reverseK + d ||
                    (k != reverseK - d && vr[reverseOffset + k - 1] < vr[reverseOffset + k + 1] - 1))
                {
                    // Up
                    x = vr[reverseOffset + k - 1];
                }
                else
                {
                    // Left
                    x = vr[reverseOffset + k + 1] - 1;
                }

                var y = x - k;

                // Traverse diagonal if possible.
                while (x > lowA && y > lowB && SourceEqualUsingOffset(x - 1, y - 1))
                {
                    x--;
                    y--;
                }

                vr[reverseOffset + k] = x;
                if (!deltaIsEven)
                {
                    // Can't have overlap here.
                }
                else if (k >= forwardK - d && k <= forwardK + d) // If ∆ is even and k + ∆ ∈ [−D, D] Then
                {
                    if (vr[reverseOffset + k] <= vf[forwardOffset + k]) // If the path overlaps the furthest reaching forward D-path in diagonal k+∆ Then
                    {
                        // The last snake of the reverse path is the middle snake.
                        x = vf[forwardOffset + k];
                        y = x - k;
                        return (x, y);
                    }
                }
            }
        }

        throw new InvalidOperationException();
    }

    private bool SourceEqualUsingOffset(int oldSourceIndex, int newSourceIndex)
        => SourceEqual(oldSourceIndex + _oldSourceOffset, newSourceIndex + _newSourceOffset);

    public abstract void Dispose();

    protected abstract int GetEditPosition(DiffEdit edit);
    protected abstract int AppendEdit(DiffEdit edit, StringBuilder builder);

    /// <summary>
    ///  Rents a char array of at least <paramref name="minimumLength"/> from the shared array pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static char[] RentArray(int minimumLength)
        => ArrayPool<char>.Shared.Rent(minimumLength);

    /// <summary>
    ///  Returns a char array to the shared array pool.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void ReturnArray(char[] array, bool clearArray = false)
        => ArrayPool<char>.Shared.Return(array, clearArray);

    /// <summary>
    ///  Ensures that <paramref name="array"/> references a char array of at least <paramref name="minimumLength"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static char[] EnsureBuffer(ref char[] array, int minimumLength)
    {
        return array.Length >= minimumLength
            ? array
            : GetNewBuffer(ref array, minimumLength);

        static char[] GetNewBuffer(ref char[] array, int minimumLength)
        {
            // We need a larger buffer. Return this array to the pool
            // and rent a new one.
            ReturnArray(array);
            array = RentArray(minimumLength);

            return array;
        }
    }

    private ImmutableArray<TextChange> ConsolidateEdits(List<DiffEdit> edits)
    {
        // Scan through the list of edits and collapse them into a minimal set of TextChanges.
        // This method assumes that there are no overlapping changes and the changes are sorted.

        using var minimalChanges = new PooledArrayBuilder<TextChange>(capacity: edits.Count);

        var start = 0;
        var end = 0;

        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        foreach (var edit in edits)
        {
            var startPosition = GetEditPosition(edit);
            if (startPosition != end)
            {
                // Previous edit's end doesn't match the new edit's start.
                // Output the text change we were tracking.
                if (start != end || builder.Length > 0)
                {
                    minimalChanges.Add(new TextChange(TextSpan.FromBounds(start, end), builder.ToString()));
                    builder.Clear();
                }

                start = startPosition;
            }

            end = AppendEdit(edit, builder);
        }

        if (start != end || builder.Length > 0)
        {
            minimalChanges.Add(new TextChange(TextSpan.FromBounds(start, end), builder.ToString()));
        }

        return minimalChanges.ToImmutableAndClear();
    }

    public virtual ImmutableArray<TextChange> GetMinimalTextChanges()
    {
        var edits = ComputeDiff();

        var changes = ConsolidateEdits(edits);

        return changes;
    }
}
