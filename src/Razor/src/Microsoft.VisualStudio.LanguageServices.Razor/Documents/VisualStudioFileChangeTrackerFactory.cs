// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Documents;

internal class VisualStudioFileChangeTrackerFactory : FileChangeTrackerFactory
{
    private readonly IErrorReporter _errorReporter;
    private readonly IVsAsyncFileChangeEx _fileChangeService;
    private readonly IProjectSnapshotManagerDispatcher _dispatcher;
    private readonly JoinableTaskContext _joinableTaskContext;

    public VisualStudioFileChangeTrackerFactory(
        IErrorReporter errorReporter,
        IVsAsyncFileChangeEx fileChangeService,
        IProjectSnapshotManagerDispatcher dispatcher,
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

        if (dispatcher is null)
        {
            throw new ArgumentNullException(nameof(dispatcher));
        }

        if (joinableTaskContext is null)
        {
            throw new ArgumentNullException(nameof(joinableTaskContext));
        }

        _errorReporter = errorReporter;
        _fileChangeService = fileChangeService;
        _dispatcher = dispatcher;
        _joinableTaskContext = joinableTaskContext;
    }

    public override FileChangeTracker Create(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException(SR.ArgumentCannotBeNullOrEmpty, nameof(filePath));
        }

        var fileChangeTracker = new VisualStudioFileChangeTracker(filePath, _errorReporter, _fileChangeService, _dispatcher, _joinableTaskContext);
        return fileChangeTracker;
    }
}
