// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [Shared]
    [ExportLanguageServiceFactory(typeof(VisualStudioDocumentTrackerFactory), RazorLanguage.Name, ServiceLayer.Default)]
    internal class DefaultVisualStudioDocumentTrackerFactoryFactory : ILanguageServiceFactory
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly ITextDocumentFactoryService _textDocumentFactory;

        [ImportingConstructor]
        public DefaultVisualStudioDocumentTrackerFactoryFactory(
            ForegroundDispatcher foregroundDispatcher,
            JoinableTaskContext joinableTaskContext,
            ITextDocumentFactoryService textDocumentFactory)
        {
            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (textDocumentFactory is null)
            {
                throw new ArgumentNullException(nameof(textDocumentFactory));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _joinableTaskContext = joinableTaskContext;
            _textDocumentFactory = textDocumentFactory;
        }

        public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
        {
            if (languageServices == null)
            {
                throw new ArgumentNullException(nameof(languageServices));
            }

            var projectManager = languageServices.GetRequiredService<ProjectSnapshotManager>();
            var workspaceEditorSettings = languageServices.GetRequiredService<WorkspaceEditorSettings>();
            var importDocumentManager = languageServices.GetRequiredService<ImportDocumentManager>();

            var projectPathProvider = languageServices.WorkspaceServices.GetRequiredService<ProjectPathProvider>();

            return new DefaultVisualStudioDocumentTrackerFactory(
                _foregroundDispatcher,
                _joinableTaskContext,
                projectManager,
                workspaceEditorSettings,
                projectPathProvider,
                _textDocumentFactory,
                importDocumentManager,
                languageServices.WorkspaceServices.Workspace);
        }
    }
}
