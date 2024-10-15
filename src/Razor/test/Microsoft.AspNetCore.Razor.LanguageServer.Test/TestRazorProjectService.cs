// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal class TestRazorProjectService(
    RemoteTextLoaderFactory remoteTextLoaderFactory,
    IProjectSnapshotManager projectManager,
    ILoggerFactory loggerFactory)
    : RazorProjectService(
        projectManager,
        CreateProjectInfoDriver(),
        remoteTextLoaderFactory,
        loggerFactory)
{
    private readonly IProjectSnapshotManager _projectManager = projectManager;

    private static IRazorProjectInfoDriver CreateProjectInfoDriver()
    {
        var mock = new StrictMock<IRazorProjectInfoDriver>();

        mock.Setup(x => x.GetLatestProjectInfo())
            .Returns([]);

        mock.Setup(x => x.AddListener(It.IsAny<IRazorProjectInfoListener>()));

        return mock.Object;
    }

    public Task<ProjectKey> AddProjectAsync(
        string filePath,
        string intermediateOutputPath,
        RazorConfiguration configuration,
        string? rootNamespace,
        string? displayName,
        CancellationToken cancellationToken)
    {
        return GetTestAccessor().AddProjectAsync(filePath, intermediateOutputPath, configuration, rootNamespace, displayName, cancellationToken);
    }

    public async Task AddDocumentToPotentialProjectsAsync(string textDocumentPath, CancellationToken cancellationToken)
    {
        foreach (var projectSnapshot in _projectManager.FindPotentialProjects(textDocumentPath))
        {
            var hostProject = ((ProjectSnapshot)projectSnapshot).HostProject;

            var normalizedProjectPath = FilePathNormalizer.NormalizeDirectory(hostProject.FilePath);
            var documents = ImmutableArray
                .CreateRange(projectSnapshot.DocumentFilePaths)
                .Add(textDocumentPath)
                .SelectAsArray(static path => new HostDocument(filePath: path, targetPath: path));

            await ((IRazorProjectInfoListener)this)
                .UpdatedAsync(new RazorProjectInfo(hostProject, projectSnapshot.ProjectWorkspaceState, documents), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
