// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor;

internal class DefaultVisualStudioRazorParserFactory : VisualStudioRazorParserFactory
{
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly ProjectSnapshotProjectEngineFactory _projectEngineFactory;
    private readonly ICompletionBroker _completionBroker;
    private readonly IErrorReporter _errorReporter;

    public DefaultVisualStudioRazorParserFactory(
        JoinableTaskContext joinableTaskContext,
        IErrorReporter errorReporter,
        ICompletionBroker completionBroker,
        ProjectSnapshotProjectEngineFactory projectEngineFactory)
    {
        _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _completionBroker = completionBroker ?? throw new ArgumentNullException(nameof(completionBroker));
        _projectEngineFactory = projectEngineFactory ?? throw new ArgumentNullException(nameof(projectEngineFactory));
    }

    public override VisualStudioRazorParser Create(VisualStudioDocumentTracker documentTracker)
    {
        if (documentTracker is null)
        {
            throw new ArgumentNullException(nameof(documentTracker));
        }

        _joinableTaskContext.AssertUIThread();

        return new DefaultVisualStudioRazorParser(
            _joinableTaskContext,
            documentTracker,
            _projectEngineFactory,
            _errorReporter,
            _completionBroker);
    }
}
