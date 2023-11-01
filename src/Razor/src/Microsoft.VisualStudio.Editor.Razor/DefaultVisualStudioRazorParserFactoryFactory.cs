// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor;

[Shared]
[ExportLanguageServiceFactory(typeof(VisualStudioRazorParserFactory), RazorLanguage.Name, ServiceLayer.Default)]
internal class DefaultVisualStudioRazorParserFactoryFactory : ILanguageServiceFactory
{
    private readonly VisualStudioCompletionBroker _completionBroker;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly IErrorReporter _errorReporter;

    [ImportingConstructor]
    public DefaultVisualStudioRazorParserFactoryFactory(
        JoinableTaskContext joinableTaskContext,
        IErrorReporter errorReporter,
        VisualStudioCompletionBroker completionBroker)
    {
        _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
        _completionBroker = completionBroker ?? throw new ArgumentNullException(nameof(completionBroker));
    }

    public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
    {
        var workspaceServices = languageServices.WorkspaceServices;
        var projectEngineFactory = workspaceServices.GetRequiredService<ProjectSnapshotProjectEngineFactory>();

        return new DefaultVisualStudioRazorParserFactory(
            _joinableTaskContext,
            _errorReporter,
            _completionBroker,
            projectEngineFactory);
    }
}
