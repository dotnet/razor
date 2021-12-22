﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal class RemoteTagHelperDeltaProvider
    {
        private readonly TagHelperResultCache _resultCache;
        private readonly object _resultIdLock = new();
        private int _currentResultId;

        public RemoteTagHelperDeltaProvider()
        {
            _resultCache = new TagHelperResultCache();
        }

        public TagHelperDeltaResult GetTagHelpersDelta(
            string projectFilePath,
            int lastResultId,
            IReadOnlyList<TagHelperDescriptor> currentTagHelpers)
        {
            var deltaApplied = true;
            if (!_resultCache.TryGet(projectFilePath, lastResultId, out var cachedTagHelpers))
            {
                deltaApplied = false;
                cachedTagHelpers = Array.Empty<TagHelperDescriptor>();
            }

            var added = GetAddedTagHelpers(currentTagHelpers, cachedTagHelpers!);
            var removed = GetRemovedTagHelpers(currentTagHelpers, cachedTagHelpers!);

            lock (_resultIdLock)
            {
                var resultId = _currentResultId;
                if (added.Count > 0 || removed.Count > 0)
                {
                    // The result actually changed, lets generate & cache a new result
                    resultId = ++_currentResultId;
                    _resultCache.Set(projectFilePath, resultId, currentTagHelpers);
                }

                var result = new TagHelperDeltaResult(deltaApplied, resultId, added, removed);
                return result;
            }
        }

        private static IReadOnlyList<TagHelperDescriptor> GetAddedTagHelpers(IReadOnlyList<TagHelperDescriptor> current, IReadOnlyList<TagHelperDescriptor> old)
        {
            if (old.Count == 0)
            {
                // Everythign is considered added when there is no collection to compare to.
                return current;
            }

            if (current.Count == 0)
            {
                // No new descriptors so can't possibly add any
                return Array.Empty<TagHelperDescriptor>();
            }

            var added = new List<TagHelperDescriptor>();

            foreach (var tagHelper in current)
            {
                if (!old.Contains(tagHelper))
                {
                    added.Add(tagHelper);
                }
            }

            return added;
        }

        private static IReadOnlyList<TagHelperDescriptor> GetRemovedTagHelpers(IReadOnlyList<TagHelperDescriptor> current, IReadOnlyList<TagHelperDescriptor> old)
        {
            if (old.Count == 0)
            {
                // Can't have anything removed if there's nothign to compare to
                return Array.Empty<TagHelperDescriptor>();
            }

            if (current.Count == 0)
            {
                // Current collection has nothing so anything in "old" must have been removed
                return old;
            }

            var removed = new List<TagHelperDescriptor>();

            foreach (var tagHelper in old)
            {
                if (!current.Contains(tagHelper))
                {
                    removed.Add(tagHelper);
                }
            }

            return removed;
        }
    }
}
