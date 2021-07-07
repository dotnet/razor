// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Documents
{
    internal class VisualStudioFileChangeTrackerFactory : FileChangeTrackerFactory
    {
        private readonly ErrorReporter _errorReporter;
        private readonly IVsAsyncFileChangeEx _fileChangeService;
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;

        public VisualStudioFileChangeTrackerFactory(
            ErrorReporter errorReporter,
            IVsAsyncFileChangeEx fileChangeService,
            SingleThreadedDispatcher singleThreadedDispatcher,
            JoinableTaskContext joinableTaskContext)
        {
            if (errorReporter is null)
            {
                throw new ArgumentNullException(nameof(errorReporter));
            }

            if (fileChangeService is null)
            {
                throw new ArgumentNullException(nameof(fileChangeService));
            }

            if (singleThreadedDispatcher is null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            _errorReporter = errorReporter;
            _fileChangeService = fileChangeService;
            _singleThreadedDispatcher = singleThreadedDispatcher;
            _joinableTaskContext = joinableTaskContext;
        }

        public override FileChangeTracker Create(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException(Resources.ArgumentCannotBeNullOrEmpty, nameof(filePath));
            }

            var fileChangeTracker = new VisualStudioFileChangeTracker(filePath, _errorReporter, _fileChangeService, _singleThreadedDispatcher, _joinableTaskContext);
            return fileChangeTracker;
        }
    }
}
