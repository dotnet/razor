﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Utilities;

/// <summary>
/// This class helps de-duplicate dynamically created strings which might otherwise lead to memory bloat.
/// </summary>
internal class StringCache
{
    // ConditionalWeakTable won't work for us because it only compares keys to Object.ReferenceEquals
    // (which won't be true because our values are loaded from JSON, not a constant).
    private readonly HashSet<Entry> _hashSet;
    private readonly object _lock = new object();
    private int _capacity;

    public StringCache(int capacity = 1024)
    {
        _capacity = capacity;
        _hashSet = new HashSet<Entry>(new ExfiltratingEqualityComparer());
    }

    public int ApproximateSize => _hashSet.Count;

    public string GetOrAddValue(string key)
    {
        lock (_lock)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (TryGetValue(key, out var result))
            {
                return result!;
            }
            else
            {
                var weakRef = new Entry(key);
                _hashSet.Add(weakRef);

                // Whenever we expand lets clean up dead references
                if (_capacity <= _hashSet.Count)
                {
                    Cleanup();
                    _capacity *= 2;
                }

                return key;
            }
        }
    }

    private void Cleanup()
    {
        _hashSet.RemoveWhere(weakRef => !weakRef.IsAlive);
    }

    private bool TryGetValue(string key, out string? value)
    {
        if (_hashSet.Contains(new Entry(key)))
        {
            var exfiltrator = (ExfiltratingEqualityComparer)_hashSet.Comparer;
            var val = exfiltrator.LastEqualValue!;
            if (val.Value.TryGetTarget(out var target))
            {
                value = target!;
                return true;
            }
            else
            {
                throw new InvalidOperationException("HashSet contained entry but we were unable to get the value from it. This should be impossible");
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// This is a gross hack to do a sneaky and get the value inside the HashSet out given the lack of any Get operations in netstandard2.0.
    /// If we ever upgrade to 2.1 delete this and just use the built in TryGetValue method.
    /// </summary>
    /// <remarks>
    /// This is fragile on the ordering of the values passed to the EqualityComparer by HashSet.
    /// If that ever switches we have to react, if it becomes indeterminate we have to abandon this strategy.
    /// </remarks>
    private class ExfiltratingEqualityComparer : IEqualityComparer<Entry>
    {
        public Entry? LastEqualValue { get; private set; }

        public bool Equals(Entry x, Entry y)
        {
            if (x.Equals(y))
            {
                LastEqualValue = x;
                return true;
            }
            else
            {
                LastEqualValue = null;
                return false;
            }
        }

        public int GetHashCode(Entry obj) => obj.GetHashCode();
    }

    private struct Entry
    {
        // In order to use HashSet we need a stable HashCode, so we have to cache it as soon as it comes in.
        // If the HashCode is unstable then entries in the HashSet become unreachable/unremovable.
        private readonly int _targetHashCode;

        private readonly WeakReference<string> _weakRef;

        public Entry(string target)
        {
            _weakRef = new WeakReference<string>(target);
            _targetHashCode = target.GetHashCode();
        }

        public bool IsAlive => _weakRef.TryGetTarget(out _);

        public bool TryGetTarget([NotNullWhen(true)] out string? target)
        {
            return _weakRef.TryGetTarget(out target);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Entry entry)
            {
                return false;
            }

            if (TryGetTarget(out var thisTarget) && entry.TryGetTarget(out var entryTarget))
            {
                return thisTarget.GetHashCode().Equals(entryTarget.GetHashCode()) &&
                       thisTarget == entryTarget;
            }

            // We lost the reference, but we need to check RefEquals to ensure that HashSet can successfully Remove items.
            // We can't compare the Entries themselves because as structs they would get Value-Boxed an RefEquals would always be false.
            return ReferenceEquals(_weakRef, entry._weakRef);
        }

        public override int GetHashCode()
        {
            return _targetHashCode;
        }
    }
}
