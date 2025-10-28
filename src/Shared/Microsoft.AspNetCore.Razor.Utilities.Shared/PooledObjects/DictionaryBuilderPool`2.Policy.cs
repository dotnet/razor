// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class DictionaryBuilderPool<TKey, TValue>
{
    private sealed class Policy(IEqualityComparer<TKey>? keyComparer) : IPooledObjectPolicy<ImmutableDictionary<TKey, TValue>.Builder>
    {
        public static readonly Policy Instance = new();

        private Policy()
            : this(keyComparer: null)
        {
        }

        public ImmutableDictionary<TKey, TValue>.Builder Create()
            => ImmutableDictionary.CreateBuilder<TKey, TValue>(keyComparer);

        public bool Return(ImmutableDictionary<TKey, TValue>.Builder builder)
        {
            builder.Clear();

            return true;
        }
    }
}
