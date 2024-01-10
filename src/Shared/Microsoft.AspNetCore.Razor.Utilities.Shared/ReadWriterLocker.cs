// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using static Microsoft.AspNetCore.Razor.ReadWriterLocker;

namespace Microsoft.AspNetCore.Razor;

internal class ReadWriterLocker
{
    // Specify recursion is supported, since an item with an upgradeable lock can still
    // get another read lock on the same thread
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public ReadLock EnterReadLock() => new ReadLock(_lock);
    public WriteLock EnterWriteLock() => new WriteLock(_lock);
    public UpgradeableReadLock EnterUpgradeableReadLock() => new UpgradeableReadLock(_lock);

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

    public struct ReadLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _rwLock;
        private bool _disposed;

        public ReadLock(ReaderWriterLockSlim rwLock)
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

    public struct WriteLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _rwLock;
        private bool _disposed;

        public WriteLock(ReaderWriterLockSlim rwLock)
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

    public struct UpgradeableReadLock : IDisposable
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

        public WriteLock EnterWriteLock()
        {
            return new WriteLock(_rwLock);
        }
    }
}

internal static class ReaderWriterLockExtensions
{
    public static ref ReadLock AsRef(this in ReadLock readLock)
        => ref Unsafe.AsRef(in readLock);

    public static ref UpgradeableReadLock AsRef(this in UpgradeableReadLock upgradeableReadLock)
        => ref Unsafe.AsRef(in upgradeableReadLock);

    public static ref WriteLock AsRef(this in WriteLock writeLock)
        => ref Unsafe.AsRef(in writeLock);
}
