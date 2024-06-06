// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.ProjectSystem;

public class RazorProjectInfoDriverTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    private static readonly HostProject s_hostProject1 = new(
        projectFilePath: "C:/path/to/project1/project1.csproj",
        intermediateOutputPath: "C:/path/to/project1/obj",
        razorConfiguration: RazorConfiguration.Default,
        rootNamespace: "TestNamespace");

    private static readonly HostDocument s_hostDocument1 = new("C:/path/to/project1/file.razor", "file.razor");

    private static readonly HostProject s_hostProject2 = new(
        projectFilePath: "C:/path/to/project2/project2.csproj",
        intermediateOutputPath: "C:/path/to/project2/obj",
        razorConfiguration: RazorConfiguration.Default,
        rootNamespace: "TestNamespace");

    private static readonly HostDocument s_hostDocument2 = new("C:/path/to/project2/file.razor", "file.razor");

    [UIFact]
    public async Task ProcessesExistingProjectsDuringInitialization()
    {
        var projectManager = CreateProjectSnapshotManager();

        await projectManager.UpdateAsync(static updater =>
        {
            updater.ProjectAdded(s_hostProject1);
            updater.DocumentAdded(s_hostProject1.Key, s_hostDocument1, CreateTextLoader("<p>Hello World</p>", s_hostDocument1.FilePath));

            updater.ProjectAdded(s_hostProject2);
            updater.DocumentAdded(s_hostProject2.Key, s_hostDocument2, CreateTextLoader("<p>Hello World</p>", s_hostDocument2.FilePath));
        });

        var (driver, testAccessor) = await CreateDriverAndInitializeAsync(projectManager);

        await testAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Sort projects by project key.
        var latestProjects = driver
            .GetLatestProjectInfo()
            .Sort((x, y) => x.ProjectKey.Id.CompareTo(y.ProjectKey.Id));

        Assert.Equal(2, latestProjects.Length);

        var projectInfo1 = latestProjects[0];
        Assert.Equal(s_hostProject1.Key, projectInfo1.ProjectKey);
        var document1 = Assert.Single(projectInfo1.Documents);
        Assert.Equal(s_hostDocument1.FilePath, document1.FilePath);

        var projectInfo2 = latestProjects[1];
        Assert.Equal(s_hostProject2.Key, projectInfo2.ProjectKey);
        var document2 = Assert.Single(projectInfo2.Documents);
        Assert.Equal(s_hostDocument2.FilePath, document2.FilePath);
    }

    [UIFact]
    public async Task ProcessesProjectsAddedAfterInitialization()
    {
        var projectManager = CreateProjectSnapshotManager();

        var (publisher, testAccessor) = await CreateDriverAndInitializeAsync(projectManager);

        await projectManager.UpdateAsync(static updater =>
        {
            updater.ProjectAdded(s_hostProject1);
            updater.DocumentAdded(s_hostProject1.Key, s_hostDocument1, CreateTextLoader("<p>Hello World</p>", s_hostDocument1.FilePath));

            updater.ProjectAdded(s_hostProject2);
            updater.DocumentAdded(s_hostProject2.Key, s_hostDocument2, CreateTextLoader("<p>Hello World</p>", s_hostDocument2.FilePath));
        });

        await testAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Sort projects by project key.
        var latestProjects = publisher
            .GetLatestProjectInfo()
            .Sort((x, y) => x.ProjectKey.Id.CompareTo(y.ProjectKey.Id));

        Assert.Equal(2, latestProjects.Length);

        var projectInfo1 = latestProjects[0];
        Assert.Equal(s_hostProject1.Key, projectInfo1.ProjectKey);
        var document1 = Assert.Single(projectInfo1.Documents);
        Assert.Equal(s_hostDocument1.FilePath, document1.FilePath);

        var projectInfo2 = latestProjects[1];
        Assert.Equal(s_hostProject2.Key, projectInfo2.ProjectKey);
        var document2 = Assert.Single(projectInfo2.Documents);
        Assert.Equal(s_hostDocument2.FilePath, document2.FilePath);
    }

    [UIFact]
    public async Task ProcessesDocumentAddedAfterInitialization()
    {
        var projectManager = CreateProjectSnapshotManager();

        await projectManager.UpdateAsync(static updater =>
        {
            updater.ProjectAdded(s_hostProject1);
        });

        var (publisher, testAccessor) = await CreateDriverAndInitializeAsync(projectManager);

        await projectManager.UpdateAsync(static updater =>
        {
            updater.DocumentAdded(s_hostProject1.Key, s_hostDocument1, CreateTextLoader("<p>Hello World</p>", s_hostDocument1.FilePath));
        });

        await testAccessor.WaitUntilCurrentBatchCompletesAsync();

        var latestProjects = publisher.GetLatestProjectInfo();

        var projectInfo1 = Assert.Single(latestProjects);
        Assert.Equal(s_hostProject1.Key, projectInfo1.ProjectKey);
        var document1 = Assert.Single(projectInfo1.Documents);
        Assert.Equal(s_hostDocument1.FilePath, document1.FilePath);
    }

    [UIFact]
    public async Task ProcessesProjectRemovedAfterInitialization()
    {
        var projectManager = CreateProjectSnapshotManager();

        await projectManager.UpdateAsync(static updater =>
        {
            updater.ProjectAdded(s_hostProject1);
        });

        var (publisher, testAccessor) = await CreateDriverAndInitializeAsync(projectManager);

        await testAccessor.WaitUntilCurrentBatchCompletesAsync();

        var latestProjects = publisher.GetLatestProjectInfo();

        var projectInfo1 = Assert.Single(latestProjects);
        Assert.Equal(s_hostProject1.Key, projectInfo1.ProjectKey);

        await projectManager.UpdateAsync(static updater =>
        {
            updater.ProjectRemoved(s_hostProject1.Key);
        });

        await testAccessor.WaitUntilCurrentBatchCompletesAsync();

        Assert.Empty(publisher.GetLatestProjectInfo());
    }

    [UIFact]
    public async Task ListenerNotifiedOfUpdates()
    {
        var projectManager = CreateProjectSnapshotManager();

        await projectManager.UpdateAsync(static updater =>
        {
            updater.ProjectAdded(s_hostProject1);
        });

        var (publisher, testAccessor) = await CreateDriverAndInitializeAsync(projectManager);

        var listener = new TestListener();
        publisher.AddListener(listener);

        await projectManager.UpdateAsync(static updater =>
        {
            updater.DocumentAdded(s_hostProject1.Key, s_hostDocument1, CreateTextLoader("<p>Hello World</p>", s_hostDocument1.FilePath));
        });

        await testAccessor.WaitUntilCurrentBatchCompletesAsync();

        Assert.Empty(listener.Removes);

        var projectInfo1 = Assert.Single(listener.Updates);
        Assert.Equal(s_hostProject1.Key, projectInfo1.ProjectKey);
        var document1 = Assert.Single(projectInfo1.Documents);
        Assert.Equal(s_hostDocument1.FilePath, document1.FilePath);
    }

    [UIFact]
    public async Task ListenerNotifiedOfRemoves()
    {
        var projectManager = CreateProjectSnapshotManager();

        await projectManager.UpdateAsync(static updater =>
        {
            updater.ProjectAdded(s_hostProject1);
        });

        var (publisher, testAccessor) = await CreateDriverAndInitializeAsync(projectManager);

        var listener = new TestListener();
        publisher.AddListener(listener);

        await projectManager.UpdateAsync(static updater =>
        {
            updater.ProjectRemoved(s_hostProject1.Key);
        });

        await testAccessor.WaitUntilCurrentBatchCompletesAsync();

        var projectKey1 = Assert.Single(listener.Removes);
        Assert.Equal(s_hostProject1.Key, projectKey1);

        Assert.Empty(listener.Updates);
    }

    private async Task<(RazorProjectInfoDriver, RazorProjectInfoDriver.TestAccessor)> CreateDriverAndInitializeAsync(
        IProjectSnapshotManager projectManager)
    {
        var driver = new RazorProjectInfoDriver(projectManager, delay: TimeSpan.FromMilliseconds(5));
        AddDisposable(driver);

        var testAccessor = driver.GetTestAccessor();

        await driver.InitializeAsync(DisposalToken);

        return (driver, testAccessor);
    }

    private static TextLoader CreateTextLoader(string content, string filePath)
    {
        var mock = new StrictMock<TextLoader>();

        var sourceText = SourceText.From(content);
        var textAndVersion = TextAndVersion.Create(sourceText, VersionStamp.Default, filePath);

        mock.Setup(x => x.LoadTextAndVersionAsync(It.IsAny<LoadTextOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(textAndVersion);

        return mock.Object;
    }

    private sealed class TestListener : IRazorProjectInfoListener
    {
        private readonly ImmutableArray<ProjectKey>.Builder _removes = ImmutableArray.CreateBuilder<ProjectKey>();
        private readonly ImmutableArray<RazorProjectInfo>.Builder _updates = ImmutableArray.CreateBuilder<RazorProjectInfo>();

        public ImmutableArray<ProjectKey> Removes => _removes.ToImmutable();
        public ImmutableArray<RazorProjectInfo> Updates => _updates.ToImmutable();

        public ValueTask RemovedAsync(ProjectKey projectKey, CancellationToken cancellationToken)
        {
            _removes.Add(projectKey);
            return default;
        }

        public ValueTask UpdatedAsync(RazorProjectInfo projectInfo, CancellationToken cancellationToken)
        {
            _updates.Add(projectInfo);
            return default;
        }
    }
}
