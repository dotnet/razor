// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor;

[Shared]
[ExportLanguageServiceFactory(typeof(VisualStudioDocumentTrackerFactory), RazorLanguage.Name, ServiceLayer.Default)]
internal class DefaultVisualStudioDocumentTrackerFactoryFactory : ILanguageServiceFactory
{
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly ITextDocumentFactoryService _textDocumentFactory;
    private readonly IProjectSnapshotProjectEngineFactory _projectEngineFactory;

    [ImportingConstructor]
    public DefaultVisualStudioDocumentTrackerFactoryFactory(
        ProjectSnapshotManagerDispatcher dispatcher,
        JoinableTaskContext joinableTaskContext,
        ITextDocumentFactoryService textDocumentFactory,
        IProjectSnapshotProjectEngineFactory projectEngineFactory)
    {
        _dispatcher = dispatcher;
        _joinableTaskContext = joinableTaskContext;
        _textDocumentFactory = textDocumentFactory;
        _projectEngineFactory = projectEngineFactory;
    }

    public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
    {
        if (languageServices is null)
        {
            throw new ArgumentNullException(nameof(languageServices));
        }

        var projectManager = languageServices.GetRequiredService<ProjectSnapshotManager>();
        var workspaceEditorSettings = languageServices.GetRequiredService<WorkspaceEditorSettings>();
        var importDocumentManager = languageServices.GetRequiredService<ImportDocumentManager>();

        var projectPathProvider = languageServices.WorkspaceServices.GetRequiredService<ProjectPathProvider>();

        return new DefaultVisualStudioDocumentTrackerFactory(
            _dispatcher,
            _joinableTaskContext,
            projectManager,
            workspaceEditorSettings,
            projectPathProvider,
            _textDocumentFactory,
            importDocumentManager,
            _projectEngineFactory);
    }
}
