// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

[Shared]
[ExportLanguageServiceFactory(typeof(ProjectSnapshotManager), RazorLanguage.Name)]
internal class DefaultProjectSnapshotManagerFactory : ILanguageServiceFactory
{
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly IEnumerable<IProjectSnapshotChangeTrigger> _triggers;
    private readonly IProjectSnapshotProjectEngineFactory _projectEngineFactory;
    private readonly IErrorReporter _errorReporter;

    [ImportingConstructor]
    public DefaultProjectSnapshotManagerFactory(
        ProjectSnapshotManagerDispatcher dispatcher,
        [ImportMany] IEnumerable<IProjectSnapshotChangeTrigger> triggers,
        IProjectSnapshotProjectEngineFactory projectEngineFactory,
        IErrorReporter errorReporter)
    {
        _dispatcher = dispatcher;
        _triggers = triggers;
        _projectEngineFactory = projectEngineFactory;
        _errorReporter = errorReporter;
    }

    public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
    {
        return new DefaultProjectSnapshotManager(
            _errorReporter,
            _triggers,
            _projectEngineFactory,
            languageServices.WorkspaceServices.Workspace,
            _dispatcher);
    }
}
