// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable
using System;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Razor.Serialization.Internal
{
    internal class StringCache
    {
        private readonly ExfiltratingEqualityComparer _comparer;
        private readonly HashSet<Entry> _hashSet;
        private readonly object _lock = new object();
        private int _capacity;

        public StringCache(int capacity = 1024)
        {
            _capacity = capacity;
            _comparer = new ExfiltratingEqualityComparer();
            _hashSet = new HashSet<Entry>(_comparer);
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
            _hashSet.RemoveWhere((weakRef) =>
            {
                return !weakRef.IsAlive;
            });
        }

        private bool TryGetValue(string key, out string? value)
        {
            if (_hashSet.Contains(new Entry(key)))
            {
                var exfiltrator = (ExfiltratingEqualityComparer)_hashSet.Comparer;
                var val = exfiltrator.LastEqualValue!;
                if (val.TryGetTarget(out var target))
                {
                    value = target!;
                    return true;
                }
                else
                {
                    // If one of our strings is out of scope probably tons of them are.
                    Cleanup();
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// This is a gross hack to do a sneaky and get the value inside the HashSet out given the lack of any Get operations in netstandard2.0.
        /// If we ever upgrade to 2.1 delete this and just use the built in.
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

            public int GetHashCode(Entry obj)
            {
                return obj.TargetHashCode;
            }
        }

        private class Entry
        {
            public readonly int TargetHashCode;

            private WeakReference<string> _weakRef;

            public Entry(string target)
            {
                _weakRef = new WeakReference<string>(target);
                TargetHashCode = target.GetHashCode();
            }

            public bool IsAlive => _weakRef.TryGetTarget(out _);

            public bool TryGetTarget(out string target)
            {
                return _weakRef.TryGetTarget(out target);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Entry entry))
                {
                    return false;
                }

                if (TryGetTarget(out var thisTarget) && entry.TryGetTarget(out var entryTarget))
                {
                    return thisTarget!.Equals(entryTarget, StringComparison.Ordinal);
                }

                // If we lost one of the references just compare the HashCodes and hope we don't have any collisions
                return TargetHashCode == entry.TargetHashCode;
            }

            public override int GetHashCode()
            {
                return TargetHashCode;
            }
        }
    }
}
