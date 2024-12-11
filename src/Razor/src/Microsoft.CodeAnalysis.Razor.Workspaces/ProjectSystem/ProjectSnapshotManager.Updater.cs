// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal partial class ProjectSnapshotManager
{
    public readonly struct Updater(ProjectSnapshotManager instance)
    {
        public ImmutableArray<ProjectKey> GetAllProjectKeys(string projectFileName)
            => instance.GetAllProjectKeys(projectFileName);
        public ImmutableArray<IProjectSnapshot> GetProjects()
            => instance.GetProjects();
        public IProjectSnapshot GetLoadedProject(ProjectKey projectKey)
            => instance.GetLoadedProject(projectKey);
        public bool TryGetLoadedProject(ProjectKey projectKey, [NotNullWhen(true)] out IProjectSnapshot? project)
            => instance.TryGetLoadedProject(projectKey, out project);

        public bool IsDocumentOpen(string documentFilePath)
            => instance.IsDocumentOpen(documentFilePath);
        public ImmutableArray<string> GetOpenDocuments()
            => instance.GetOpenDocuments();

        public void AddDocument(ProjectKey projectKey, HostDocument document, TextLoader textLoader)
            => instance.AddDocument(projectKey, document, textLoader);

        public void DocumentRemoved(ProjectKey projectKey, HostDocument document)
            => instance.DocumentRemoved(projectKey, document);

        public void UpdateDocumentText(ProjectKey projectKey, string documentFilePath, TextLoader textLoader)
            => instance.UpdateDocumentText(projectKey, documentFilePath, textLoader);

        public void UpdateDocumentText(ProjectKey projectKey, string documentFilePath, SourceText sourceText)
            => instance.UpdateDocumentText(projectKey, documentFilePath, sourceText);

        public void OpenDocument(ProjectKey projectKey, string documentFilePath, SourceText sourceText)
            => instance.OpenDocument(projectKey, documentFilePath, sourceText);

        public void CloseDocument(ProjectKey projectKey, string documentFilePath, TextLoader textLoader)
            => instance.CloseDocument(projectKey, documentFilePath, textLoader);

        public void AddProject(HostProject project)
            => instance.AddProject(project);

        public void RemoveProject(ProjectKey projectKey)
            => instance.RemoveProject(projectKey);

        public void ProjectConfigurationChanged(HostProject project)
            => instance.ProjectConfigurationChanged(project);

        public void ProjectWorkspaceStateChanged(ProjectKey projectKey, ProjectWorkspaceState projectWorkspaceState)
            => instance.ProjectWorkspaceStateChanged(projectKey, projectWorkspaceState);

        public void SolutionOpened()
            => instance.SolutionOpened();

        public void SolutionClosed()
            => instance.SolutionClosed();
    }
}
