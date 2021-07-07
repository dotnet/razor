// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using OmniSharp.FileWatching;
using OmniSharp.Services;

namespace OmniSharp
{
    public static class TestOmniSharpWorkspace
    {
        private static readonly object WorkspaceLock = new object();

        public static OmniSharpWorkspace Create()
        {
            lock (WorkspaceLock)
            {
                var factory = LoggerFactory.Create((b) => { });
                var hostServicesAggregator = new HostServicesAggregator(Enumerable.Empty<IHostServicesProvider>(), factory);
                var workspace = new OmniSharpWorkspace(hostServicesAggregator, factory, TestFileSystemWatcher.Instance);

                return workspace;
            }
        }

        private class TestFileSystemWatcher : IFileSystemWatcher
        {
            public static readonly TestFileSystemWatcher Instance = new TestFileSystemWatcher();

            private TestFileSystemWatcher()
            {
            }

            public void Watch(string pathOrExtension, FileSystemNotificationCallback callback)
            {
            }
        }
    }
}
