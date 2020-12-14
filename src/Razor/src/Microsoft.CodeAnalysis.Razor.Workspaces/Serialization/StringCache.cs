// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    /// <summary>
    /// The purpose of this class is to avoid permanently storing duplicate strings which were read in from JSON.
    /// </summary>
    internal class StringCache
    {
        private readonly WeakValueDictionary<string, string> _table = new WeakValueDictionary<string, string>();
        private readonly object _lock = new object();

        public string GetOrAdd(string str)
        {
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            lock (_lock)
            {
                if (_table.TryGetValue(str, out var value))
                {
                    return value;
                }
                else
                {
                    _table.Add(str, str);
                    return str;
                }
            }
        }

        // ConditionalWeakTable won't work for us because it only compares keys to Object.ReferenceEquals.
        private class WeakValueDictionary<TKey, TValue> where TValue : class
        {
            // Your basic project.razor.json using our template have ~800 entries,
            // so lets start the dictionary out large
            private int _capacity = 800;

            private readonly Dictionary<TKey, WeakReference<TValue>> _dict;

            public WeakValueDictionary()
            {
                _dict = new Dictionary<TKey, WeakReference<TValue>>(_capacity);
            }

            public bool TryGetValue(TKey key, out TValue value)
            {
                if (key is null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                if (_dict.TryGetValue(key, out var weak))
                {
                    if (!weak.TryGetTarget(out value))
                    {
                        // This means we lost all references to the value, time to remove the entry.
                        _dict.Remove(key);
                        return false;
                    }

                    return true;
                }

                // We didn't find the value
                value = null;
                return false;
            }

            public void Add(TKey key, TValue value)
            {
                if (_dict.Count == _capacity)
                {
                    Cleanup();

                    if (_dict.Count == _capacity)
                    {
                        _capacity = _dict.Count * 2;
                    }
                }

                var weak = new WeakReference<TValue>(value);
                _dict.Add(key, weak);
            }

            private void Cleanup()
            {
                for (var i = _dict.Count; i >= 0; i--)
                {
                    var kvp = _dict.ElementAt(i);
                    if (kvp.Value is null)
                    {
                        // Entry exists but has null value (should never happen)
                        throw new NotImplementedException($"{kvp.Key} is in the dictionary, but has no value.");
                    }

                    if (!kvp.Value.TryGetTarget(out _))
                    {
                        // No references remain for this, remove it from the list entirely
                        _dict.Remove(kvp.Key);
                    }
                }
            }
        }
    }
}
