// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    /// <summary>
    /// Caches WeakReferences of the given values.
    /// </summary>
    /// <remarks>
    /// This class was created to serve as a kind of non-permanent string.Intern,
    /// preventing memory bloat from duplicate non-constant strings without
    /// permanently adding them to the programs static memory like string.Intern would.
    /// </remarks>
    /// <typeparam name="TKey">The type to be stored in this HashSet</typeparam>
    public sealed class ConditionalWeakHashSet<TKey> : IEnumerable<TKey>
        where TKey : class
    {
        // Lifetimes of keys:
        // Inserting a key into the HashSet will not
        // prevent the key from dying. Once the key dies, the dictionary automatically removes
        // the entry.
        //
        // Thread safety guarantees:
        // ConditionalWeakTable is fully thread-safe and requires no
        // additional locking to be done by callers.
        //
        // OOM guarantees:
        // Will not corrupt unmanaged handle table on OOM. No guarantees
        // about managed weak table consistency. Native handles reclamation
        // may be delayed until appdomain shutdown.

        // Your basic project.razor.json using our template have ~800 entries,
        // so lets start the dictionary out large
        private const int InitialCapacity = 1024;  // Initial length of the table. Must be a power of two.
        private readonly object _lock;          // This lock protects all mutation of data in the table.  Readers do not take this lock.
        private volatile Container _container;  // The actual storage for the table; swapped out as the table grows.
        private int _activeEnumeratorRefCount;  // The number of outstanding enumerators on the table

        public ConditionalWeakHashSet()
        {
            _lock = new object();
            _container = new Container(this);
        }

        /// <summary>Gets the cached value of the specified key.</summary>
        /// <param name="key">key of the value to find. Cannot be null.</param>
        /// <param name="value">
        /// If the key is found, contains the cached instance of the key upon method return.
        /// If the key is not found, contains default(<typeparamref name="TKey" />).
        /// </param>
        /// <returns>Returns "true" if key was found, "false" otherwise.</returns>
        /// <remarks>
        /// The key may get garbaged collected during the TryGetValue operation. If so, TryGetValue
        /// may at its discretion, return "false" and set "value" to the default (as if the key was not present.)
        /// </remarks>
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TKey value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return _container.TryGetValueWorker(key, out value);
        }

        /// <summary>
        /// Gets the cached value of the specified key if it has already been cached, or stores then value and then returns it.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TKey GetOrAddValue(TKey key)
        {
            lock (_lock)
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
                else
                {
                    CreateEntry(key);
                    return key;
                }
            }
        }

        /// <summary>Adds a key to the table.</summary>
        /// <param name="key">key to add. May not be null.</param>
        /// <remarks>
        /// If the key is already entered into the dictionary, this method throws an exception.
        /// The key may get garbage collected during the Add() operation. If so, Add()
        /// has the right to consider any prior entries successfully removed and add a new entry without
        /// throwing an exception.
        /// </remarks>
        public void Add(TKey key)
        {
            lock (_lock)
            {
                var entryIndex = _container.FindEntry(key, out _);
                if (entryIndex != -1)
                {
                    throw new ArgumentException($"Attempting to add duplicate");
                }

                CreateEntry(key);
            }
        }

        /// <summary>Gets an enumerator for the table.</summary>
        /// <remarks>
        /// The returned enumerator will not extend the lifetime of
        /// any object pairs in the table, other than the one that's Current.  It will not return entries
        /// that have already been collected, nor will it return entries added after the enumerator was
        /// retrieved.  It may not return all entries that were present when the enumerat was retrieved,
        /// however, such as not returning entries that were collected or removed after the enumerator
        /// was retrieved but before they were enumerated.
        /// </remarks>
        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
        {
            lock (_lock)
            {
                var c = _container;
                return c is null || c.FirstFreeEntry == 0 ?
                    ((IEnumerable<TKey>)Array.Empty<TKey>()).GetEnumerator() :
                    new Enumerator(this);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<TKey>)this).GetEnumerator();

        /// <summary>Provides an enumerator for the table.</summary>
        private sealed class Enumerator : IEnumerator<TKey>
        {
            // The enumerator would ideally hold a reference to the Container and the end index within that
            // container.  However, the safety of the CWT depends on the only reference to the Container being
            // from the CWT itself; the Container then employs a two-phase finalization scheme, where the first
            // phase nulls out that parent CWT's reference, guaranteeing that the second time it's finalized there
            // can be no other existing references to it in use that would allow for concurrent usage of the
            // native handles with finalization.  We would break that if we allowed this Enumerator to hold a
            // reference to the Container.  Instead, the Enumerator holds a reference to the CWT rather than to
            // the Container, and it maintains the CWT._activeEnumeratorRefCount field to track whether there
            // are outstanding enumerators that have yet to be disposed/finalized.  If there aren't any, the CWT
            // behaves as it normally does.  If there are, certain operations are affected, in particular resizes.
            // Normally when the CWT is resized, it enumerates the contents of the table looking for indices that
            // contain entries which have been collected or removed, and it frees those up, effectively moving
            // down all subsequent entries in the container (not in the existing container, but in a replacement).
            // This, however, would cause the enumerator's understanding of indices to break.  So, as long as
            // there is any outstanding enumerator, no compaction is performed.

            private ConditionalWeakHashSet<TKey>? _table; // parent table, set to null when disposed
            private readonly int _maxIndexInclusive;            // last index in the container that should be enumerated
            private int _currentIndex;                          // the current index into the container
            private TKey? _current;        // the current entry set by MoveNext and returned from Current

            public Enumerator(ConditionalWeakHashSet<TKey> table)
            {
                Debug.Assert(Monitor.IsEntered(table._lock), "Must hold the _lock lock to construct the enumerator");
                Debug.Assert(table._container.FirstFreeEntry > 0, "Should have returned an empty enumerator instead");

                // Store a reference to the parent table and increase its active enumerator count.
                _table = table;
                Debug.Assert(table._activeEnumeratorRefCount >= 0, "Should never have a negative ref count before incrementing");
                table._activeEnumeratorRefCount++;

                // Store the max index to be enumerated.
                _maxIndexInclusive = table._container.FirstFreeEntry - 1;
                _currentIndex = -1;
            }

            ~Enumerator()
            {
                Dispose();
            }

            public void Dispose()
            {
                // Use an interlocked operation to ensure that only one thread can get access to
                // the _table for disposal and thus only decrement the ref count once.
                var table = Interlocked.Exchange(ref _table, null);
                if (table != null)
                {
                    // Ensure we don't keep the last current alive unnecessarily
                    _current = default;

                    // Decrement the ref count that was incremented when constructed
                    lock (table._lock)
                    {
                        table._activeEnumeratorRefCount--;
                        Debug.Assert(table._activeEnumeratorRefCount >= 0, "Should never have a negative ref count after decrementing");
                    }

                    // Finalization is purely to decrement the ref count.  We can suppress it now.
                    GC.SuppressFinalize(this);
                }
            }

            public bool MoveNext()
            {
                // Start by getting the current table.  If it's already been disposed, it will be null.
                var table = _table;
                if (table != null)
                {
                    // Once have the table, we need to lock to synchronize with other operations on
                    // the table, like adding.
                    lock (table._lock)
                    {
                        // From the table, we have to get the current container.  This could have changed
                        // since we grabbed the enumerator, but the index-to-pair mapping should not have
                        // due to there being at least one active enumerator.  If the table (or rather its
                        // container at the time) has already been finalized, this will be null.
                        var c = table._container;
                        if (c != null)
                        {
                            // We have the container.  Find the next entry to return, if there is one.
                            // We need to loop as we may try to get an entry that's already been removed
                            // or collected, in which case we try again.
                            while (_currentIndex < _maxIndexInclusive)
                            {
                                _currentIndex++;
                                if (c.TryGetEntry(_currentIndex, out var key))
                                {
                                    _current = key;
                                    return true;
                                }
                            }
                        }
                    }
                }

                // Nothing more to enumerate.
                return false;
            }

            public TKey Current
            {
                get
                {
                    if (_currentIndex < 0)
                    {
                        throw new InvalidOperationException("Enum Operation can't happen");
                    }
                    return _current!;
                }
            }

            object? IEnumerator.Current => Current;

            public void Reset() { }
        }

        /// <summary>Worker for adding a new key/value pair. Will resize the container if it is full.</summary>
        /// <param name="key"></param>
        private void CreateEntry(TKey key)
        {
            Debug.Assert(Monitor.IsEntered(_lock));

            var c = _container;
            if (!c.HasCapacity)
            {
                _container = c = c.Resize();
            }
            c.CreateEntryNoResize(key);
        }

        private static bool IsPowerOfTwo(int value) => (value > 0) && ((value & (value - 1)) == 0);

        //--------------------------------------------------------------------------------------------
        // Entry can be in one of four states:
        //
        //    - Unused (stored with an index _firstFreeEntry and above)
        //         depHnd.IsAllocated == false
        //         hashCode == <dontcare>
        //         next == <dontcare>)
        //
        //    - Used with live key (linked into a bucket list where _buckets[hashCode & (_buckets.Length - 1)] points to first entry)
        //         depHnd.IsAllocated == true, depHnd.GetPrimary() != null
        //         hashCode == RuntimeHelpers.GetHashCode(depHnd.GetPrimary()) & int.MaxValue
        //         next links to next Entry in bucket.
        //
        //    - Used with dead key (linked into a bucket list where _buckets[hashCode & (_buckets.Length - 1)] points to first entry)
        //         depHnd.IsAllocated == true, depHnd.GetPrimary() is null
        //         hashCode == <notcare>
        //         next links to next Entry in bucket.
        //
        //    - Has been removed from the table (by a call to Remove)
        //         depHnd.IsAllocated == true, depHnd.GetPrimary() == <notcare>
        //         hashCode == -1
        //         next links to next Entry in bucket.
        //
        // The only difference between "used with live key" and "used with dead key" is that
        // depHnd.GetPrimary() returns null. The transition from "used with live key" to "used with dead key"
        // happens asynchronously as a result of normal garbage collection. The dictionary itself
        // receives no notification when this happens.
        //
        // When the dictionary grows the _entries table, it scours it for expired keys and does not
        // add those to the new container.
        //--------------------------------------------------------------------------------------------
        private struct Entry
        {
            public WeakReference<TKey> depHnd;      // Holds key and value using a weak reference for the key and a strong reference
                                                    // for the value that is traversed only if the key is reachable without going through the value.
            public int HashCode;    // Cached copy of key's hashcode
            public int Next;        // Index of next entry, -1 if last
        }

        /// <summary>
        /// Container holds the actual data for the table.  A given instance of Container always has the same capacity.  When we need
        /// more capacity, we create a new Container, copy the old one into the new one, and discard the old one.  This helps enable lock-free
        /// reads from the table, as readers never need to deal with motion of entries due to rehashing.
        /// </summary>
        private sealed class Container
        {
            private readonly ConditionalWeakHashSet<TKey> _parent;  // the ConditionalWeakTable with which this container is associated
            private readonly int[] _buckets;                // _buckets[hashcode & (_buckets.Length - 1)] contains index of the first entry in bucket (-1 if empty)
            private readonly Entry[] _entries;              // the table entries containing the stored dependency handles
            private int _firstFreeEntry;           // _firstFreeEntry < _entries.Length => table has capacity,  entries grow from the bottom of the table.
            private bool _invalid;                 // flag detects if OOM or other background exception threw us out of the lock.

            internal Container(ConditionalWeakHashSet<TKey> parent)
            {
                Debug.Assert(IsPowerOfTwo(InitialCapacity));

                const int Size = InitialCapacity;
                _buckets = new int[Size];
                for (var i = 0; i < _buckets.Length; i++)
                {
                    _buckets[i] = -1;
                }
                _entries = new Entry[Size];

                // Only store the parent after all of the allocations have happened successfully.
                // Otherwise, as part of growing or clearing the container, we could end up allocating
                // a new Container that fails (OOMs) part way through construction but that gets finalized
                // and ends up clearing out some other container present in the associated CWT.
                _parent = parent;
            }

            private Container(ConditionalWeakHashSet<TKey> parent, int[] buckets, Entry[] entries, int firstFreeEntry)
            {
                Debug.Assert(buckets.Length == entries.Length);
                Debug.Assert(IsPowerOfTwo(buckets.Length));

                _parent = parent;
                _buckets = buckets;
                _entries = entries;
                _firstFreeEntry = firstFreeEntry;
            }

            internal bool HasCapacity => _firstFreeEntry < _entries.Length;

            internal int FirstFreeEntry => _firstFreeEntry;

            /// <summary>Worker for adding a new key/value pair. Container must NOT be full.</summary>
            internal void CreateEntryNoResize(TKey key)
            {
                Debug.Assert(HasCapacity);

                VerifyIntegrity();
                _invalid = true;

                var hashCode = key.GetHashCode() & int.MaxValue;
                var newEntry = _firstFreeEntry++;

                _entries[newEntry].HashCode = hashCode;
                _entries[newEntry].depHnd = new WeakReference<TKey>(key);
                var bucket = hashCode & (_buckets.Length - 1);
                _entries[newEntry].Next = _buckets[bucket];

                // This write must be volatile, as we may be racing with concurrent readers.  If they see
                // the new entry, they must also see all of the writes earlier in this method.
                Volatile.Write(ref _buckets[bucket], newEntry);

                _invalid = false;
            }

            /// <summary>Worker for finding a key/value pair. Must hold _lock.</summary>
            internal bool TryGetValueWorker(TKey key, [MaybeNullWhen(false)] out TKey value)
            {
                var entryIndex = FindEntry(key, out value);
                return entryIndex != -1;
            }

            /// <summary>
            /// Returns -1 if not found (if key expires during FindEntry, this can be treated as "not found.").
            /// Must hold _lock, or be prepared to retry the search while holding _lock.
            /// </summary>
            internal int FindEntry(TKey key, out TKey? keyValue)
            {
                var hashCode = key.GetHashCode() & int.MaxValue;
                var bucket = hashCode & (_buckets.Length - 1);
                for (var entriesIndex = Volatile.Read(ref _buckets[bucket]); entriesIndex != -1; entriesIndex = _entries[entriesIndex].Next)
                {
                    if (_entries[entriesIndex].HashCode == hashCode && _entries[entriesIndex].depHnd.TryGetTarget(out var locKey) && locKey.Equals(key))
                    {
                        GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.
                        keyValue = locKey;
                        return entriesIndex;
                    }
                }

                GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.
                keyValue = null;
                return -1;
            }

            /// <summary>Gets the entry at the specified entry index.</summary>
            internal bool TryGetEntry(int index, [NotNullWhen(true)] out TKey? key)
            {
                if (index < _entries.Length)
                {
                    var hasTarget = _entries[index].depHnd.TryGetTarget(out var oKey);
                    GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.

                    if (hasTarget)
                    {
                        key = Unsafe.As<TKey>(oKey);
                        return true;
                    }
                }

                key = default;
                return false;
            }

            /// <summary>Resize, and scrub expired keys off bucket lists. Must hold _lock.</summary>
            /// <remarks>
            /// _firstEntry is less than _entries.Length on exit, that is, the table has at least one free entry.
            /// </remarks>
            internal Container Resize()
            {
                Debug.Assert(!HasCapacity);

                var hasExpiredEntries = false;
                var newSize = _buckets.Length;

                if (_parent is null || _parent._activeEnumeratorRefCount == 0)
                {
                    // If any expired or removed keys exist, we won't resize.
                    // If there any active enumerators, though, we don't want
                    // to compact and thus have no expired entries.
                    for (var entriesIndex = 0; entriesIndex < _entries.Length; entriesIndex++)
                    {
                        ref var entry = ref _entries[entriesIndex];

                        if (entry.HashCode == -1)
                        {
                            // the entry was removed
                            hasExpiredEntries = true;
                            break;
                        }

                        if (entry.depHnd.TryGetTarget(out var key) && key is null)
                        {
                            // the entry has expired
                            hasExpiredEntries = true;
                            break;
                        }
                    }
                }

                if (!hasExpiredEntries)
                {
                    // Not necessary to check for overflow here, the attempt to allocate new arrays will throw
                    newSize = _buckets.Length * 2;
                }

                return Resize(newSize);
            }

            internal Container Resize(int newSize)
            {
                Debug.Assert(newSize >= _buckets.Length);
                Debug.Assert(IsPowerOfTwo(newSize));

                // Reallocate both buckets and entries and rebuild the bucket and entries from scratch.
                // This serves both to scrub entries with expired keys and to put the new entries in the proper bucket.
                var newBuckets = new int[newSize];
                for (var bucketIndex = 0; bucketIndex < newBuckets.Length; bucketIndex++)
                {
                    newBuckets[bucketIndex] = -1;
                }
                var newEntries = new Entry[newSize];
                var newEntriesIndex = 0;
                var activeEnumerators = _parent != null && _parent._activeEnumeratorRefCount > 0;

                // Migrate existing entries to the new table.
                if (activeEnumerators)
                {
                    // There's at least one active enumerator, which means we don't want to
                    // remove any expired/removed entries, in order to not affect existing
                    // entries indices.  Copy over the entries while rebuilding the buckets list,
                    // as the buckets are dependent on the buckets list length, which is changing.
                    for (; newEntriesIndex < _entries.Length; newEntriesIndex++)
                    {
                        ref var oldEntry = ref _entries[newEntriesIndex];
                        ref var newEntry = ref newEntries[newEntriesIndex];
                        var hashCode = oldEntry.HashCode;

                        newEntry.HashCode = hashCode;
                        newEntry.depHnd = oldEntry.depHnd;
                        var bucket = hashCode & (newBuckets.Length - 1);
                        newEntry.Next = newBuckets[bucket];
                        newBuckets[bucket] = newEntriesIndex;
                    }
                }
                else
                {
                    // There are no active enumerators, which means we want to compact by
                    // removing expired/removed entries.
                    for (var entriesIndex = 0; entriesIndex < _entries.Length; entriesIndex++)
                    {
                        ref var oldEntry = ref _entries[entriesIndex];
                        var hashCode = oldEntry.HashCode;
                        var depHnd = oldEntry.depHnd;
                        if (hashCode != -1 && depHnd.TryGetTarget(out var key))
                        {
                            if (key != null)
                            {
                                ref var newEntry = ref newEntries[newEntriesIndex];

                                // Entry is used and has not expired. Link it into the appropriate bucket list.
                                newEntry.HashCode = hashCode;
                                newEntry.depHnd = depHnd;
                                var bucket = hashCode & (newBuckets.Length - 1);
                                newEntry.Next = newBuckets[bucket];
                                newBuckets[bucket] = newEntriesIndex;
                                newEntriesIndex++;
                            }
                            else
                            {
                                // Pretend the item was removed, so that this container's finalizer
                                // will clean up this dependent handle.
                                Volatile.Write(ref oldEntry.HashCode, -1);
                            }
                        }
                    }
                }

                // Create the new container.  We want to transfer the responsibility of freeing the handles from
                // the old container to the new container, and also ensure that the new container isn't finalized
                // while the old container may still be in use.  As such, we store a reference from the old container
                // to the new one, which will keep the new container alive as long as the old one is.
                var newContainer = new Container(_parent!, newBuckets, newEntries, newEntriesIndex);
                if (activeEnumerators)
                {
                    // If there are active enumerators, both the old container and the new container may be storing
                    // the same entries with -1 hash codes, which the finalizer will clean up even if the container
                    // is not the active container for the table.  To prevent that, we want to stop the old container
                    // from being finalized, as it no longer has any responsibility for any cleanup.
                    GC.SuppressFinalize(this);
                }

                GC.KeepAlive(this); // ensure we don't get finalized while accessing DependentHandles.

                return newContainer;
            }

            private void VerifyIntegrity()
            {
                if (_invalid)
                {
                    throw new InvalidOperationException("Collection is corrupted");
                }
            }
        }
    }
}
