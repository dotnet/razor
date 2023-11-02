// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor;

[Shared]
[ExportLanguageServiceFactory(typeof(VisualStudioRazorParserFactory), RazorLanguage.Name, ServiceLayer.Default)]
internal class DefaultVisualStudioRazorParserFactoryFactory : ILanguageServiceFactory
{
    private readonly ICompletionBroker _completionBroker;
    private readonly IProjectSnapshotProjectEngineFactory _projectEngineFactory;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly IErrorReporter _errorReporter;

    [ImportingConstructor]
    public DefaultVisualStudioRazorParserFactoryFactory(
        JoinableTaskContext joinableTaskContext,
        IErrorReporter errorReporter,
        ICompletionBroker completionBroker,
        IProjectSnapshotProjectEngineFactory projectEngineFactory)
    {
        _joinableTaskContext = joinableTaskContext;
        _errorReporter = errorReporter;
        _completionBroker = completionBroker;
        _projectEngineFactory = projectEngineFactory;
    }

    public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
    {
        return new DefaultVisualStudioRazorParserFactory(
            _joinableTaskContext,
            _errorReporter,
            _completionBroker,
            _projectEngineFactory);
    }
}
