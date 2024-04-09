﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Razor.IntegrationTests.InProcess;
using Xunit;
using Microsoft.VisualStudio.Razor.LanguageClient;

namespace Microsoft.VisualStudio.Extensibility.Testing;

[TestService]
internal partial class RazorProjectSystemInProcess
{
    public async Task WaitForLSPServerActivatedAsync(CancellationToken cancellationToken)
    {
        await WaitForLSPServerActivationStatusAsync(active: true, cancellationToken);
    }

    public async Task WaitForLSPServerDeactivatedAsync(CancellationToken cancellationToken)
    {
        await WaitForLSPServerActivationStatusAsync(active: false, cancellationToken);
    }

    private async Task WaitForLSPServerActivationStatusAsync(bool active, CancellationToken cancellationToken)
    {
        var tracker = await TestServices.Shell.GetComponentModelServiceAsync<ILspServerActivationTracker>(cancellationToken);
        await Helper.RetryAsync(ct =>
        {
            return Task.FromResult(tracker.IsActive == active);
        }, TimeSpan.FromMilliseconds(50), cancellationToken);
    }

    public async Task WaitForProjectFileAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        var projectManager = await TestServices.Shell.GetComponentModelServiceAsync<IProjectSnapshotManager>(cancellationToken);
        Assert.NotNull(projectManager);
        await Helper.RetryAsync(ct =>
        {
            var projectKeys = projectManager.GetAllProjectKeys(projectFilePath);
            if (projectKeys.Length == 0)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(projectManager.TryGetLoadedProject(projectKeys[0], out _));
        }, TimeSpan.FromMilliseconds(100), cancellationToken);
    }

    public async Task WaitForRazorFileInProjectAsync(string projectFilePath, string filePath, CancellationToken cancellationToken)
    {
        var projectSnapshotManager = await TestServices.Shell.GetComponentModelServiceAsync<IProjectSnapshotManager>(cancellationToken);
        Assert.NotNull(projectSnapshotManager);
        await Helper.RetryAsync(ct =>
        {
            var projectKeys = projectSnapshotManager.GetAllProjectKeys(projectFilePath);
            if (projectKeys.Length == 0 ||
                !projectSnapshotManager.TryGetLoadedProject(projectKeys[0], out var project))
            {
                return Task.FromResult(false);
            }

            var document = project.GetDocument(filePath);

            return Task.FromResult(document is not null);
        }, TimeSpan.FromMilliseconds(100), cancellationToken);
    }

    public async Task WaitForCSharpVirtualDocumentAsync(string razorFilePath, CancellationToken cancellationToken)
    {
        var documentManager = await TestServices.Shell.GetComponentModelServiceAsync<LSPDocumentManager>(cancellationToken);

        var uri = new Uri(razorFilePath, UriKind.Absolute);
        await Helper.RetryAsync(ct =>
        {
            if (documentManager.TryGetDocument(uri, out var snapshot))
            {
                if (snapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var virtualDocument))
                {
                    var result = virtualDocument.ProjectKey.Id is not null &&
                        virtualDocument.Snapshot.Length > 0;
                    return Task.FromResult(result);
                }
            }

            return Task.FromResult(false);

        }, TimeSpan.FromMilliseconds(100), cancellationToken);
    }
}

