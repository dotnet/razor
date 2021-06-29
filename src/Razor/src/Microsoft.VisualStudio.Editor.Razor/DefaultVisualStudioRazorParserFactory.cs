// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal class DefaultVisualStudioRazorParserFactory : VisualStudioRazorParserFactory
    {
        private readonly ForegroundDispatcher _dispatcher;
        private readonly ProjectSnapshotProjectEngineFactory _projectEngineFactory;
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly VisualStudioCompletionBroker _completionBroker;
        private readonly ErrorReporter _errorReporter;

        public DefaultVisualStudioRazorParserFactory(
            ForegroundDispatcher dispatcher,
            JoinableTaskContext joinableTaskContext,
            ErrorReporter errorReporter,
            VisualStudioCompletionBroker completionBroker,
            ProjectSnapshotProjectEngineFactory projectEngineFactory)
        {
            if (dispatcher is null)
            {
                throw new ArgumentNullException(nameof(dispatcher));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (errorReporter is null)
            {
                throw new ArgumentNullException(nameof(errorReporter));
            }

            if (completionBroker is null)
            {
                throw new ArgumentNullException(nameof(completionBroker));
            }

            if (projectEngineFactory is null)
            {
                throw new ArgumentNullException(nameof(projectEngineFactory));
            }

            _dispatcher = dispatcher;
            _joinableTaskContext = joinableTaskContext;
            _errorReporter = errorReporter;
            _completionBroker = completionBroker;
            _projectEngineFactory = projectEngineFactory;
        }

        public override VisualStudioRazorParser Create(VisualStudioDocumentTracker documentTracker)
        {
            if (documentTracker == null)
            {
                throw new ArgumentNullException(nameof(documentTracker));
            }

            _dispatcher.AssertForegroundThread();

            var parser = new DefaultVisualStudioRazorParser(
                _dispatcher,
                _joinableTaskContext,
                documentTracker,
                _projectEngineFactory,
                _errorReporter,
                _completionBroker);
            return parser;
        }
    }
}
