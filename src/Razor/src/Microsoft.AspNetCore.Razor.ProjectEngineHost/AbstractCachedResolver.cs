// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor;

internal abstract partial class AbstractCachedResolver<T>
{
    private readonly object _gate = new();
    private int _currentResultId;

    private record Entry(int ResultId, ImmutableArray<Checksum> Checksums);
    private readonly MemoryCache<ProjectId, Entry> _projectResultCache = new();

    protected abstract T? TryGet(Checksum checksum);

    protected abstract ValueTask<ImmutableArray<Checksum>> GetCurrentChecksumsAsync(Project project, CancellationToken cancellationToken);

    public ImmutableArray<T> GetValues(ProjectId projectKey, int resultId)
    {
        if (!TryGetCachedChecksums(projectKey, resultId, out var cachedChecksums))
        {
            return [];
        }

        using var _ = ArrayBuilderPool<T>.GetPooledObject(out var builder);
        foreach (var checksum in cachedChecksums)
        {
            var value = TryGet(checksum);
            if (value is null)
            {
                continue;
            }

            builder.Add(value);
        }

        return builder.ToImmutableArray();
    }

    public async ValueTask<DeltaResult> GetDeltaAsync(Project project, int? lastResultIdNullable, CancellationToken cancellationToken)
    {
        var lastResultId = lastResultIdNullable ?? -1;

        var currentChecksums = await GetCurrentChecksumsAsync(project, cancellationToken).ConfigureAwait(false);
        var cacheHit = TryGetCachedChecksums(project.Id, lastResultId, out var cachedChecksums);

        if (!cacheHit)
        {
            cachedChecksums = [];
        }

        ImmutableArray<Checksum> added;
        ImmutableArray<Checksum> removed;

        if (cachedChecksums.Length < currentChecksums.Length)
        {
            added = Delta.Compute(cachedChecksums, currentChecksums);

            // No need to call TagHelperDelta.Compute again if we know there aren't any removed
            var anyRemoved = currentChecksums.Length - cachedChecksums.Length != added.Length;
            removed = anyRemoved ? Delta.Compute(currentChecksums, cachedChecksums) : ImmutableArray<Checksum>.Empty;
        }
        else
        {
            removed = Delta.Compute(currentChecksums, cachedChecksums);

            // No need to call TagHelperDelta.Compute again if we know there aren't any added
            var anyAdded = cachedChecksums.Length - currentChecksums.Length != removed.Length;
            added = anyAdded ? Delta.Compute(cachedChecksums, currentChecksums) : ImmutableArray<Checksum>.Empty;
        }

        lock (_gate)
        {
            var newResultId = _currentResultId;
            if (added.Length > 0 || removed.Length > 0)
            {
                // The result actually changed, lets generate & cache a new result
                newResultId = ++_currentResultId;
                SetCachedChecksums(project.Id, newResultId, currentChecksums);
            }
            else if (cacheHit)
            {
                // Re-use existing result ID if we've hit he cache so next time we get asked we hit again.
                newResultId = lastResultId;
            }

            return new DeltaResult(cacheHit, newResultId, added, removed);
        }
    }

    public bool TryGetCachedChecksums(ProjectId projectKey, int resultId, out ImmutableArray<Checksum> cachedChecksums)
    {
        if (!_projectResultCache.TryGetValue(projectKey, out var cachedResult))
        {
            cachedChecksums = default;
            return false;
        }
        else if (cachedResult.ResultId != resultId)
        {
            // We don't know about the result that's being requested. Fallback to uncached behavior.
            cachedChecksums = default;
            return false;
        }

        cachedChecksums = cachedResult.Checksums;
        return true;
    }

    public bool TryGetId(ProjectId projectKey, out int resultId)
    {
        if (!_projectResultCache.TryGetValue(projectKey, out var cachedResult))
        {
            resultId = -1;
            return false;
        }

        resultId = cachedResult.ResultId;
        return true;
    }

    public void SetCachedChecksums(ProjectId projectKey, int resultId, ImmutableArray<Checksum> checksums)
    {
        var cacheEntry = new Entry(resultId, checksums);
        _projectResultCache.Set(projectKey, cacheEntry);
    }
}
