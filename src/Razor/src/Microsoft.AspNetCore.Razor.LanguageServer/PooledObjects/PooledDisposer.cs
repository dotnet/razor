// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.PooledObjects
{
    [NonCopyable]
    internal readonly struct PooledDisposer<TPoolable> : IDisposable
        where TPoolable : class, IPooled
    {
        private readonly TPoolable _pooledObject;

        public PooledDisposer(TPoolable instance)
            => _pooledObject = instance;

        void IDisposable.Dispose()
            => _pooledObject?.Free();
    }
}
