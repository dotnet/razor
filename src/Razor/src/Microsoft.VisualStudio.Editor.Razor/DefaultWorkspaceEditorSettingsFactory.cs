// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [Shared]
    [ExportLanguageServiceFactory(typeof(WorkspaceEditorSettings), RazorLanguage.Name)]
    internal class DefaultWorkspaceEditorSettingsFactory : ILanguageServiceFactory
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly EditorSettingsManager _editorSettingsManager;

        [ImportingConstructor]
        public DefaultWorkspaceEditorSettingsFactory(ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher, EditorSettingsManager editorSettingsManager)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (editorSettingsManager is null)
            {
                throw new ArgumentNullException(nameof(editorSettingsManager));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _editorSettingsManager = editorSettingsManager;
        }

        public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
        {
            if (languageServices is null)
            {
                throw new ArgumentNullException(nameof(languageServices));
            }

            return new DefaultWorkspaceEditorSettings(_projectSnapshotManagerDispatcher, _editorSettingsManager);
        }
    }
}
