﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.TextDifferencing;

//
// This class implements the linear space variation of the difference algorithm described in
// "An O(ND) Difference Algorithm and its Variations" by Eugene W. Myers
//
// Note: Some variable names in this class are not be fully compliant with the C# naming guidelines
// in the interest of using the same terminology discussed in the paper for ease of understanding.
//
internal abstract partial class TextDiffer
{
    protected abstract int OldSourceLength { get; }
    protected abstract int NewSourceLength { get; }

    protected abstract bool SourceEqual(int oldSourceIndex, int newSourceIndex);

    protected List<DiffEdit> ComputeDiff()
    {
        var edits = new List<DiffEdit>(capacity: 4);
        var builder = new DiffEditBuilder(edits);

        // Initialize the vectors to use for forward and reverse searches.
        var max = NewSourceLength + OldSourceLength;
        using var vf = new IntArray((2 * max) + 1);
        using var vr = new IntArray((2 * max) + 1);

        ComputeDiffRecursive(builder, 0, OldSourceLength, 0, NewSourceLength, vf, vr);

        return edits;
    }

    private void ComputeDiffRecursive(DiffEditBuilder edits, int lowA, int highA, int lowB, int highB, IntArray vf, IntArray vr)
    {
        while (lowA < highA && lowB < highB && SourceEqual(lowA, lowB))
        {
            // Skip equal text at the start.
            lowA++;
            lowB++;
        }

        while (lowA < highA && lowB < highB && SourceEqual(highA - 1, highB - 1))
        {
            // Skip equal text at the end.
            highA--;
            highB--;
        }

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
                while (x < highA && y < highB && SourceEqual(x, y))
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
                while (x > lowA && y > lowB && SourceEqual(x - 1, y - 1))
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

        throw Assumes.NotReachable();
    }
}
