// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.AspNetCore.Razor;

internal class LockFactory
{
    // Specify recursion is supported, since an item with an upgradeable lock can still
    // get another read lock on the same thread
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
    private static readonly TimeSpan s_timeout = TimeSpan.FromMinutes(2);

    public ReadOnlyLock EnterReadLock() => new ReadOnlyLock(_lock);
    public WriteOnlyLock EnterWriteLock() => new WriteOnlyLock(_lock);
    public UpgradeAbleReadLock EnterUpgradeAbleReadLock() => new UpgradeAbleReadLock(_lock);

    public void EnsureNoWriteLock()
    {
        if (_lock.IsWriteLockHeld)
        {
            throw new InvalidOperationException("Expected no write lock to be held");
        }
    }

    public ref struct ReadOnlyLock
    {
        private readonly ReaderWriterLockSlim _rwLock;
        private bool _disposed;

        public ReadOnlyLock(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
            if (!_rwLock.TryEnterReadLock(s_timeout))
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
            if (!_rwLock.TryEnterWriteLock(s_timeout))
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

    public ref struct UpgradeAbleReadLock
    {
        private readonly ReaderWriterLockSlim _rwLock;
        private bool _disposed;

        public UpgradeAbleReadLock(ReaderWriterLockSlim rwLock)
        {
            _rwLock = rwLock;
            if (!_rwLock.TryEnterUpgradeableReadLock(s_timeout))
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

        public WriteOnlyLock GetWriteLock()
        {
            return new WriteOnlyLock(_rwLock);
        }
    }
}
