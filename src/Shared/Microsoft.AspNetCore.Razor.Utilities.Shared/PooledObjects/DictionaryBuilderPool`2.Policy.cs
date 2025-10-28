// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class DictionaryBuilderPool<TKey, TValue>
{
    private sealed class Policy(IEqualityComparer<TKey>? keyComparer) : PooledObjectPolicy
    {
        public static readonly Policy Default = new();

        private Policy()
            : this(keyComparer: null)
        {
        }

        public override ImmutableDictionary<TKey, TValue>.Builder Create()
            => ImmutableDictionary.CreateBuilder<TKey, TValue>(keyComparer);

        public override bool Return(ImmutableDictionary<TKey, TValue>.Builder builder)
        {
            builder.Clear();

            return true;
        }
    }
}
