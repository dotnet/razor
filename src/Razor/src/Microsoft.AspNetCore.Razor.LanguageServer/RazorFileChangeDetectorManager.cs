// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorFileChangeDetectorManager : IDisposable
    {
        private readonly WorkspaceDirectoryResolver _workspaceDirectoryResolver;
        private readonly IEnumerable<IFileChangeDetector> _fileChangeDetectors;
        private readonly object _disposeLock = new object();
        private bool _disposed;

        public RazorFileChangeDetectorManager(
            WorkspaceDirectoryResolver workspaceDirectoryResolver,
            IEnumerable<IFileChangeDetector> fileChangeDetectors)
        {
            if (workspaceDirectoryResolver is null)
            {
                throw new ArgumentNullException(nameof(workspaceDirectoryResolver));
            }

            if (fileChangeDetectors is null)
            {
                throw new ArgumentNullException(nameof(fileChangeDetectors));
            }

            _workspaceDirectoryResolver = workspaceDirectoryResolver;
            _fileChangeDetectors = fileChangeDetectors;
        }

        public async Task InitializedAsync()
        {
            // Initialized request, this occurs once the server and client have agreed on what sort of features they both support. It only happens once.

            var workspaceDirectory = _workspaceDirectoryResolver.Resolve();

            foreach (var fileChangeDetector in _fileChangeDetectors)
            {
                // We create a dummy cancellation token for now. Have an issue to pass through the cancellation token in the O# lib: https://github.com/OmniSharp/csharp-language-server-protocol/issues/200
                var cancellationToken = CancellationToken.None;
                await fileChangeDetector.StartAsync(workspaceDirectory, cancellationToken);
            }

            lock (_disposeLock)
            {
                if (_disposed)
                {
                    // Got disposed while starting our file change detectors. We need to re-stop our change detectors.
                    Stop();
                }
            }
        }

        private void Stop()
        {
            foreach (var fileChangeDetector in _fileChangeDetectors)
            {
                fileChangeDetector.Stop();
            }
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                Stop();
            }
        }
    }
}
