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
        private readonly HashSet<WeakReference<string>> _hashSet;
        private readonly object _lock = new object();

        public StringCache()
        {
            _comparer = new ExfiltratingEqualityComparer();
            _hashSet = new HashSet<WeakReference<string>>(_comparer);
        }

        public string GetOrAddValue(string key)
        {
            lock (_lock)
            {
                if (key is null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                if (TryGetValue(_hashSet, key, out var result))
                {
                    return result!;
                }
                else
                {
                    var weakRef = new WeakReference<string>(key);
                    _hashSet.Add(weakRef);
                    return key;
                }
            }
        }

        private static bool TryGetValue(HashSet<WeakReference<string>> hashSet, string key, out string? value)
        {
            if (hashSet.Contains(new WeakReference<string>(key)))
            {
                var exfiltrator = (ExfiltratingEqualityComparer)hashSet.Comparer;
                var val = exfiltrator.LastEqualValue!;
                if (val.TryGetTarget(out var target))
                {
                    value = target!;
                    return true;
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
        private class ExfiltratingEqualityComparer : IEqualityComparer<WeakReference<string>>
        {
            public WeakReference<string>? LastEqualValue { get; private set; }

            public bool Equals(WeakReference<string> x, WeakReference<string> y)
            {
                x.TryGetTarget(out var x1);
                y.TryGetTarget(out var y1);

                if (x1 is null || y1 is null)
                {
                    return false;
                }

                if (x1.Equals(y1, StringComparison.Ordinal))
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

            public int GetHashCode(WeakReference<string> obj)
            {
                if (obj.TryGetTarget(out var val))
                {
                    return val.GetHashCode();
                }

                return obj.GetHashCode();
            }
        }
    }
}
