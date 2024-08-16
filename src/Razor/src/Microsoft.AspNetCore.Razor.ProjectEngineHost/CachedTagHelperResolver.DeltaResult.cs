// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;

#if DEBUG
using System.Diagnostics;
#endif

namespace Microsoft.AspNetCore.Razor;

internal partial class CachedTagHelperResolver
{
    public sealed record class DeltaResult(
        bool IsDelta,
        int ResultId,
        ImmutableArray<Checksum> Added,
        ImmutableArray<Checksum> Removed)
    {
        public ImmutableArray<Checksum> Apply(ImmutableArray<Checksum> baseChecksums)
        {
            if (Added.Length == 0 && Removed.Length == 0)
            {
                return baseChecksums;
            }

            using var result = new PooledArrayBuilder<Checksum>(capacity: baseChecksums.Length + Added.Length - Removed.Length);

            result.AddRange(Added);
            result.AddRange(Delta.Compute(Removed, baseChecksums));

#if DEBUG
            // Ensure that there are no duplicate tag helpers in the result.
            using var set = new PooledHashSet<Checksum>(capacity: result.Count);

            foreach (var item in result)
            {
                Debug.Assert(set.Add(item), $"{nameof(DeltaResult)}.{nameof(Apply)} should not contain any duplicates!");
            }
#endif

            return result.DrainToImmutable();
        }
    }
}
