// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;

namespace Microsoft.VisualStudio.LanguageServer.Protocol;

internal static class VsLspFactory
{
    private static readonly Position s_undefinedPosition = new(-1, -1);

    private static readonly Range s_undefinedRange = new()
    {
        Start = s_undefinedPosition,
        End = s_undefinedPosition
    };

    public static Range UndefinedRange
    {
        get
        {
            var undefinedRange = s_undefinedRange;

            // Since Range is mutable, it's possible that something might modify it. If that happens, we should know!
            Debug.Assert(
                undefinedRange.Start.Line == -1 &&
                undefinedRange.Start.Character == -1 &&
                undefinedRange.End.Line == -1 &&
                undefinedRange.End.Character == -1,
                $"{nameof(VsLspFactory)}.{nameof(UndefinedRange)} has been corrupted. Current value: {undefinedRange.ToDisplayString()}");

            return undefinedRange;
        }
    }
}
