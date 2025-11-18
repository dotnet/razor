// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Utilities;

/// <summary>
///  A thread-safe cache implementation that uses weak references to store values, allowing them to be garbage collected
///  when no longer referenced elsewhere. The cache periodically cleans up dead weak references to prevent unbounded growth.
/// </summary>
/// <typeparam name="TKey">The type of keys used to identify cached values. Must be non-null.</typeparam>
/// <typeparam name="TValue">The type of values stored in the cache. Must be a reference type.</typeparam>
/// <remarks>
///  This cache is designed for scenarios where you want to cache expensive-to-create objects but allow them to be
///  garbage collected when memory pressure occurs. The cache will automatically clean up dead references when the
///  number of add operations reaches the specified cleanup threshold.
/// </remarks>
internal class CleanableWeakCache<TKey, TValue>
    where TKey : notnull
    where TValue : class?
{
    /// <summary>
    ///  The underlying dictionary that maps keys to weak references containing the cached values.
    /// </summary>
    private readonly Dictionary<TKey, WeakReference<TValue>> _cacheMap = [];

    /// <summary>
    ///  Synchronization object to ensure thread-safe access to the cache.
    /// </summary>
#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    /// <summary>
    ///  The number of add operations that must occur before triggering a cleanup of dead weak references.
    /// </summary>
    private readonly int _cleanUpThreshold;

    /// <summary>
    ///  Counter tracking the number of add operations since the last cleanup was performed.
    /// </summary>
    private int _addsSinceLastCleanUp;

    /// <summary>
    ///  Initializes a new instance of the <see cref="CleanableWeakCache{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="cleanUpThreshold">
    ///  The number of add operations that must occur before triggering automatic cleanup of dead weak references.
    ///  Must be non-negative.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when <paramref name="cleanUpThreshold"/> is negative.
    /// </exception>
    public CleanableWeakCache(int cleanUpThreshold)
    {
        ArgHelper.ThrowIfNegativeOrZero(cleanUpThreshold);

        _cleanUpThreshold = cleanUpThreshold;
    }

    /// <summary>
    ///  Gets the value associated with the specified key, or adds the provided value if the key is not found
    ///  or the previously cached value has been garbage collected.
    /// </summary>
    /// <param name="key">The key of the value to get or add.</param>
    /// <param name="value">The value to add if the key is not found or the cached value is no longer available.</param>
    /// <returns>
    ///  The existing value if found and still alive, otherwise the provided <paramref name="value"/>.
    /// </returns>
    public TValue GetOrAdd(TKey key, TValue value)
    {
        lock (_lock)
        {
            // Try to add the value or get the existing one. If null is returned, use the provided value.
            return TryAddOrGet_NoLock(key, value) ?? value;
        }
    }

    /// <summary>
    ///  Gets the value associated with the specified key, or adds a new value created by the factory function
    ///  if the key is not found or the previously cached value has been garbage collected.
    /// </summary>
    /// <param name="key">The key of the value to get or add.</param>
    /// <param name="valueFactory">A factory function to create the value if it needs to be added to the cache.</param>
    /// <returns>
    ///  The existing value if found and still alive, otherwise a new value created by <paramref name="valueFactory"/>.
    /// </returns>
    public TValue GetOrAdd(TKey key, Func<TValue> valueFactory)
    {
        // First check without creating the value.
        lock (_lock)
        {
            if (TryGet_NoLock(key, out var value))
            {
                return value;
            }
        }

        // Create the value outside the lock to avoid holding the lock
        // while creating a potentially expensive object.
        var newValue = valueFactory();

        // Second check and add atomically
        lock (_lock)
        {
            // Double-check in case another thread added it
            if (TryGet_NoLock(key, out var existingValue))
            {
                return existingValue;
            }

            // Add our newly created value
            TryAddOrGet_NoLock(key, newValue);
            return newValue;
        }
    }

    /// <summary>
    ///  Gets the value associated with the specified key, or adds a new value created by the factory function
    ///  using the provided argument if the key is not found or the previously cached value has been garbage collected.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument passed to the value factory function.</typeparam>
    /// <param name="key">The key of the value to get or add.</param>
    /// <param name="arg">The argument to pass to the value factory function.</param>
    /// <param name="valueFactory">A factory function to create the value using the provided argument.</param>
    /// <returns>
    ///  The existing value if found and still alive, otherwise a new value created by <paramref name="valueFactory"/>.
    /// </returns>
    public TValue GetOrAdd<TArg>(TKey key, TArg arg, Func<TArg, TValue> valueFactory)
    {
        // First check without creating the value.
        lock (_lock)
        {
            // First, try to get an existing value
            if (TryGet_NoLock(key, out var value))
            {
                return value;
            }
        }

        // Create the value outside the lock to avoid holding the lock
        // while creating a potentially expensive object.
        var newValue = valueFactory(arg);

        // Second check and add atomically
        lock (_lock)
        {
            // Double-check in case another thread added it
            if (TryGet_NoLock(key, out var existingValue))
            {
                return existingValue;
            }

            // Add our newly created value
            TryAddOrGet_NoLock(key, newValue);
            return newValue;
        }
    }

    /// <summary>
    ///  Attempts to add the specified key-value pair to the cache.
    /// </summary>
    /// <param name="key">The key of the value to add.</param>
    /// <param name="value">The value to add to the cache.</param>
    /// <returns>
    ///  <see langword="true"/> if the key-value pair was successfully added;
    ///  <see langword="false"/> if a live value already exists for the specified key.
    /// </returns>
    public bool TryAdd(TKey key, TValue value)
    {
        lock (_lock)
        {
            // Returns true if TryAddOrGet returns null (meaning the value was added)
            return TryAddOrGet_NoLock(key, value) is null;
        }
    }

    /// <summary>
    ///  Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">
    ///  When this method returns, contains the value associated with the specified key if found and still alive;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if a live value was found for the specified key; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGet(TKey key, [NotNullWhen(true)] out TValue? value)
    {
        lock (_lock)
        {
            return TryGet_NoLock(key, out value);
        }
    }

    /// <summary>
    ///  Internal method that attempts to add a value to the cache or retrieve an existing one.
    ///  This method assumes the caller already holds the lock.
    /// </summary>
    /// <param name="key">The key of the value to add or get.</param>
    /// <param name="value">The value to add if no existing value is found.</param>
    /// <returns>
    ///  The existing live value if one was found; otherwise, <see langword="null"/> indicating the new value was added.
    /// </returns>
    /// <remarks>
    ///  This method increments the add counter and triggers cleanup if the threshold is reached.
    /// </remarks>
    private TValue? TryAddOrGet_NoLock(TKey key, TValue value)
    {
        // Increment add counter and trigger cleanup if threshold is reached
        if (++_addsSinceLastCleanUp >= _cleanUpThreshold)
        {
            CleanUpDeadObjects_NoLock();
        }

        // Check if the key already exists in the cache
        if (!_cacheMap.TryGetValue(key, out var weakRef))
        {
            // Key doesn't exist, add the new value
            _cacheMap.Add(key, new(value));
            return null; // Indicates the value was successfully added
        }

        // Key exists, check if the weak reference still has a live target
        if (!weakRef.TryGetTarget(out var existingValue))
        {
            // The target was garbage collected, replace it with the new value
            weakRef.SetTarget(value);
            return null; // Indicates the value was successfully added
        }

        // Return the existing live value
        return existingValue;
    }

    /// <summary>
    ///  Internal method that attempts to retrieve a value from the cache.
    ///  This method assumes the caller already holds the lock.
    /// </summary>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">
    ///  When this method returns, contains the value if found and still alive; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if a live value was found; otherwise, <see langword="false"/>.
    /// </returns>
    private bool TryGet_NoLock(TKey key, [NotNullWhen(true)] out TValue? value)
    {
        // Check if the key exists and the weak reference still has a live target
        if (_cacheMap.TryGetValue(key, out var weakRef) &&
            weakRef.TryGetTarget(out value))
        {
            return true;
        }

        // Key not found or target was garbage collected
        value = null;
        return false;
    }

    /// <summary>
    ///  Removes all cache entries whose weak references no longer have live targets (i.e., have been garbage collected).
    ///  This method assumes the caller already holds the lock.
    /// </summary>
    /// <remarks>
    ///  This method resets the add counter to zero after cleanup is complete.
    /// </remarks>
    private void CleanUpDeadObjects_NoLock()
    {
        // Use a memory builder to collect keys of dead weak references
        using var deadKeys = new MemoryBuilder<TKey>(initialCapacity: _cacheMap.Count, clearArray: true);

        // Identify all keys with dead weak references
        foreach (var (key, weakRef) in _cacheMap)
        {
            if (!weakRef.TryGetTarget(out _))
            {
                deadKeys.Append(key);
            }
        }

        // Remove all dead entries from the cache
        foreach (var key in deadKeys.AsMemory().Span)
        {
            _cacheMap.Remove(key);
        }

        // Reset the add counter since we just performed cleanup
        _addsSinceLastCleanUp = 0;
    }
}
