﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.AspNetCore.Razor;

internal class ReadWriterLocker
{
    // Specify recursion is supported, since an item with an upgradeable lock can still
    // get another read lock on the same thread
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public ReadOnlyLock EnterReadLock() => new ReadOnlyLock(_lock);
    public WriteOnlyLock EnterWriteLock() => new WriteOnlyLock(_lock);
    public UpgradeableReadLock EnterUpgradeAbleReadLock() => new UpgradeableReadLock(_lock);

    public void EnsureNoWriteLock()
    {
        if (_lock.IsWriteLockHeld)
        {
            throw new InvalidOperationException("Expected no write lock to be held");
        }
    }

    private static readonly TimeSpan s_maxTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
    private static readonly TimeSpan s_timeout =
#if DEBUG
        s_maxTimeout;
#else
        TimeSpan.FromSeconds(30);
#endif

    private static TimeSpan GetTimeout()
    {
        if (Debugger.IsAttached)
        {
            return s_maxTimeout;
        }

        return s_timeout;
    }

    public ref struct ReadOnlyLock
    {
        private readonly ReaderWriterLockSlim _rwLock;
        private bool _disposed;

        public ReadOnlyLock(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
            if (!_rwLock.TryEnterReadLock(GetTimeout()))
            {
                throw new InvalidOperationException("Failed getting a read lock");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _rwLock.ExitReadLock();
        }
    }

    public ref struct WriteOnlyLock
    {
        private readonly ReaderWriterLockSlim _rwLock;
        private bool _disposed;

        public WriteOnlyLock(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
            if (!_rwLock.TryEnterWriteLock(GetTimeout()))
            {
                throw new InvalidOperationException("Failed getting a write lock");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _rwLock.ExitWriteLock();
        }
    }

    public ref struct UpgradeableReadLock
    {
        private readonly ReaderWriterLockSlim _rwLock;
        private bool _disposed;

        public UpgradeableReadLock(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
            if (!_rwLock.TryEnterUpgradeableReadLock(GetTimeout()))
            {
                throw new InvalidOperationException("Failed getting an upgradeable read lock");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _rwLock.ExitUpgradeableReadLock();
        }

        public WriteOnlyLock EnterWriteLock()
        {
            return new WriteOnlyLock(_rwLock);
        }
    }
}
