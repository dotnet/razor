// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal partial class DefaultProjectSnapshotManager
{
    private class LockFactory
    {
        // Specify recursion is supported, since an item with an upgradeable lock can still
        // get another read lock on the same thread
        protected readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);
        private static readonly TimeSpan s_timeout = TimeSpan.FromMinutes(2);

        public IDisposable GetReadLock() => new ReadOnlyLock(_lock);
        public IDisposable GetWriteLock() => new WriteOnlyLock(_lock);
        public UpgradeAbleReadLock GetUpgradeAbleReadLock() => new UpgradeAbleReadLock(_lock);

        public void EnsureNoWriteLock()
        {
            if (_lock.IsWriteLockHeld)
            {
                throw new InvalidOperationException("Expected no write lock to be held");
            }
        }

        private class ReadOnlyLock : IDisposable
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

        private class WriteOnlyLock : IDisposable
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

        public class UpgradeAbleReadLock : IDisposable
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

            public IDisposable GetWriteLock()
            {
                return new WriteOnlyLock(_rwLock);
            }
        }
    }
}
