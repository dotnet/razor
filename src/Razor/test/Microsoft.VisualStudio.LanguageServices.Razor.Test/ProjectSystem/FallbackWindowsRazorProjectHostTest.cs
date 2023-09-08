﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ItemCollection = Microsoft.VisualStudio.ProjectSystem.ItemCollection;
using ItemReference = Microsoft.CodeAnalysis.Razor.ProjectSystem.ManagedProjectSystemSchema.ItemReference;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

public class FallbackWindowsRazorProjectHostTest : ProjectSnapshotManagerDispatcherWorkspaceTestBase
{
    private readonly ItemCollection _referenceItems;
    private readonly TestProjectSnapshotManager _projectManager;
    private readonly ProjectConfigurationFilePathStore _projectConfigurationFilePathStore;
    private readonly ItemCollection _contentItems;
    private readonly ItemCollection _noneItems;

    public FallbackWindowsRazorProjectHostTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _projectManager = new TestProjectSnapshotManager(Workspace, Dispatcher);

        var projectConfigurationFilePathStore = new Mock<ProjectConfigurationFilePathStore>(MockBehavior.Strict);
        projectConfigurationFilePathStore.Setup(s => s.Remove(It.IsAny<ProjectKey>())).Verifiable();
        _projectConfigurationFilePathStore = projectConfigurationFilePathStore.Object;

        _referenceItems = new ItemCollection(ManagedProjectSystemSchema.ResolvedCompilationReference.SchemaName);
        _contentItems = new ItemCollection(ManagedProjectSystemSchema.ContentItem.SchemaName);
        _noneItems = new ItemCollection(ManagedProjectSystemSchema.NoneItem.SchemaName);
    }

    [Fact]
    public void GetChangedAndRemovedDocuments_ReturnsChangedContentAndNoneItems()
    {
        // Arrange
        var afterChangeContentItems = new ItemCollection(ManagedProjectSystemSchema.ContentItem.SchemaName);
        _contentItems.Item("Index.cshtml", new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "NewIndex.cshtml",
            [ItemReference.FullPathPropertyName] = "C:\\From\\Index.cshtml",
        });
        var afterChangeNoneItems = new ItemCollection(ManagedProjectSystemSchema.NoneItem.SchemaName);
        _noneItems.Item("About.cshtml", new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "NewAbout.cshtml",
            [ItemReference.FullPathPropertyName] = "C:\\From\\About.cshtml",
        });
        var services = new TestProjectSystemServices("C:\\To\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var changes = new TestProjectChangeDescription[]
        {
             afterChangeContentItems.ToChange(_contentItems.ToSnapshot()),
             afterChangeNoneItems.ToChange(_noneItems.ToSnapshot()),
        };
        var update = services.CreateUpdate(changes).Value;

        // Act
        var result = host.GetChangedAndRemovedDocuments(update);

        // Assert
        Assert.Collection(
            result,
            document =>
            {
                Assert.Equal("C:\\From\\Index.cshtml", document.FilePath);
                Assert.Equal("C:\\To\\NewIndex.cshtml", document.TargetPath);
            },
            document =>
            {
                Assert.Equal("C:\\From\\About.cshtml", document.FilePath);
                Assert.Equal("C:\\To\\NewAbout.cshtml", document.TargetPath);
            });
    }

    [Fact]
    public void GetCurrentDocuments_ReturnsContentAndNoneItems()
    {
        // Arrange
        _contentItems.Item("Index.cshtml", new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "NewIndex.cshtml",
            [ItemReference.FullPathPropertyName] = "C:\\From\\Index.cshtml",
        });
        _noneItems.Item("About.cshtml", new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "NewAbout.cshtml",
            [ItemReference.FullPathPropertyName] = "C:\\From\\About.cshtml",
        });
        var services = new TestProjectSystemServices("C:\\To\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var changes = new TestProjectChangeDescription[]
        {
             _contentItems.ToChange(),
             _noneItems.ToChange(),
        };
        var update = services.CreateUpdate(changes).Value;

        // Act
        var result = host.GetCurrentDocuments(update);

        // Assert
        Assert.Collection(
            result,
            document =>
            {
                Assert.Equal("C:\\From\\Index.cshtml", document.FilePath);
                Assert.Equal("C:\\To\\NewIndex.cshtml", document.TargetPath);
            },
            document =>
            {
                Assert.Equal("C:\\From\\About.cshtml", document.FilePath);
                Assert.Equal("C:\\To\\NewAbout.cshtml", document.TargetPath);
            });
    }

    // This is for the legacy SDK case, we don't support components.
    [Fact]
    public void GetCurrentDocuments_IgnoresDotRazorFiles()
    {
        // Arrange
        _contentItems.Item("Index.razor", new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "NewIndex.razor",
            [ItemReference.FullPathPropertyName] = "C:\\From\\Index.razor",
        });
        _noneItems.Item("About.razor", new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "NewAbout.razor",
            [ItemReference.FullPathPropertyName] = "C:\\From\\About.razor",
        });
        var services = new TestProjectSystemServices("C:\\To\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var changes = new TestProjectChangeDescription[]
        {
             _contentItems.ToChange(),
             _noneItems.ToChange(),
        };
        var update = services.CreateUpdate(changes).Value;

        // Act
        var result = host.GetCurrentDocuments(update);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void TryGetRazorDocument_NoFilePath_ReturnsFalse()
    {
        // Arrange
        var services = new TestProjectSystemServices("C:\\To\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var itemState = new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "Index.cshtml",
        }.ToImmutableDictionary();

        // Act
        var result = host.TryGetRazorDocument(itemState, out var document);

        // Assert
        Assert.False(result);
        Assert.Null(document);
    }

    [Fact]
    public void TryGetRazorDocument_NonRazorFilePath_ReturnsFalse()
    {
        // Arrange
        var services = new TestProjectSystemServices("C:\\Path\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var itemState = new Dictionary<string, string>()
        {
            [ItemReference.FullPathPropertyName] = "C:\\Path\\site.css",
        }.ToImmutableDictionary();

        // Act
        var result = host.TryGetRazorDocument(itemState, out var document);

        // Assert
        Assert.False(result);
        Assert.Null(document);
    }

    [Fact]
    public void TryGetRazorDocument_NonRazorTargetPath_ReturnsFalse()
    {
        // Arrange
        var services = new TestProjectSystemServices("C:\\Path\\To\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var itemState = new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "site.html",
            [ItemReference.FullPathPropertyName] = "C:\\Path\\From\\Index.cshtml",
        }.ToImmutableDictionary();

        // Act
        var result = host.TryGetRazorDocument(itemState, out var document);

        // Assert
        Assert.False(result);
        Assert.Null(document);
    }

    [Fact]
    public void TryGetRazorDocument_JustFilePath_ReturnsTrue()
    {
        // Arrange
        var expectedPath = "C:\\Path\\Index.cshtml";
        var services = new TestProjectSystemServices("C:\\Path\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var itemState = new Dictionary<string, string>()
        {
            [ItemReference.FullPathPropertyName] = expectedPath,
        }.ToImmutableDictionary();

        // Act
        var result = host.TryGetRazorDocument(itemState, out var document);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedPath, document.FilePath);
        Assert.Equal(expectedPath, document.TargetPath);
    }

    [Fact]
    public void TryGetRazorDocument_LinkedFilepath_ReturnsTrue()
    {
        // Arrange
        var expectedFullPath = "C:\\Path\\From\\Index.cshtml";
        var expectedTargetPath = "C:\\Path\\To\\Index.cshtml";
        var services = new TestProjectSystemServices("C:\\Path\\To\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var itemState = new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "Index.cshtml",
            [ItemReference.FullPathPropertyName] = expectedFullPath,
        }.ToImmutableDictionary();

        // Act
        var result = host.TryGetRazorDocument(itemState, out var document);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedFullPath, document.FilePath);
        Assert.Equal(expectedTargetPath, document.TargetPath);
    }

    [Fact]
    public void TryGetRazorDocument_SetsLegacyFileKind()
    {
        // Arrange
        var expectedFullPath = "C:\\Path\\From\\Index.cshtml";
        var expectedTargetPath = "C:\\Path\\To\\Index.cshtml";
        var services = new TestProjectSystemServices("C:\\Path\\To\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var itemState = new Dictionary<string, string>()
        {
            [ItemReference.LinkPropertyName] = "Index.cshtml",
            [ItemReference.FullPathPropertyName] = expectedFullPath,
        }.ToImmutableDictionary();

        // Act
        var result = host.TryGetRazorDocument(itemState, out var document);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedFullPath, document.FilePath);
        Assert.Equal(expectedTargetPath, document.TargetPath);
        Assert.Equal(FileKinds.Legacy, document.FileKind);
    }

    [UIFact]
    public async Task FallbackRazorProjectHost_UIThread_CreateAndDispose_Succeeds()
    {
        // Arrange
        var services = new TestProjectSystemServices("C:\\To\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        // Act & Assert
        await host.LoadAsync();
        Assert.Empty(_projectManager.GetProjects());

        await host.DisposeAsync();
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task FallbackRazorProjectHost_BackgroundThread_CreateAndDispose_Succeeds()
    {
        // Arrange
        var services = new TestProjectSystemServices("Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        // Act & Assert
        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        await Task.Run(async () => await host.DisposeAsync());
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact] // This can happen if the .xaml files aren't included correctly.
    public async Task OnProjectChanged_NoRulesDefined()
    {
        // Arrange
        var changes = new TestProjectChangeDescription[]
        {
        };

        var services = new TestProjectSystemServices("Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager)
        {
            AssemblyVersion = new Version(2, 0),
        };

        // Act & Assert
        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task OnProjectChanged_ReadsProperties_InitializesProject()
    {
        // Arrange
        _referenceItems.Item("c:\\nuget\\Microsoft.AspNetCore.Mvc.razor.dll");
        _contentItems.Item("Index.cshtml", new Dictionary<string, string>()
        {
            [ItemReference.FullPathPropertyName] = "C:\\Path\\Index.cshtml",
        });
        _noneItems.Item("About.cshtml", new Dictionary<string, string>()
        {
            [ItemReference.FullPathPropertyName] = "C:\\Path\\About.cshtml",
        });

        var changes = new TestProjectChangeDescription[]
        {
             _referenceItems.ToChange(),
             _contentItems.ToChange(),
             _noneItems.ToChange(),
        };

        var services = new TestProjectSystemServices("C:\\Path\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager)
        {
            AssemblyVersion = new Version(2, 0), // Mock for reading the assembly's version
        };

        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        // Act
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert
        var snapshot = Assert.Single(_projectManager.GetProjects());
        Assert.Equal("C:\\Path\\Test.csproj", snapshot.FilePath);
        Assert.Same(FallbackRazorConfiguration.MVC_2_0, snapshot.Configuration);

        Assert.Collection(
            snapshot.DocumentFilePaths,
            filePath => Assert.Equal("C:\\Path\\Index.cshtml", filePath),
            filePath => Assert.Equal("C:\\Path\\About.cshtml", filePath));

        await Task.Run(async () => await host.DisposeAsync());
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task OnProjectChanged_NoAssemblyFound_DoesNotIniatializeProject()
    {
        // Arrange
        var changes = new TestProjectChangeDescription[]
        {
             _referenceItems.ToChange(),
        };
        var services = new TestProjectSystemServices("Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        // Act
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert
        Assert.Empty(_projectManager.GetProjects());

        await Task.Run(async () => await host.DisposeAsync());
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task OnProjectChanged_AssemblyFoundButCannotReadVersion_DoesNotIniatializeProject()
    {
        // Arrange
        _referenceItems.Item("c:\\nuget\\Microsoft.AspNetCore.Mvc.razor.dll");

        var changes = new TestProjectChangeDescription[]
        {
             _referenceItems.ToChange(),
        };

        var services = new TestProjectSystemServices("Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        // Act
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert
        Assert.Empty(_projectManager.GetProjects());

        await Task.Run(async () => await host.DisposeAsync());
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task OnProjectChanged_UpdateProject_Succeeds()
    {
        // Arrange
        _referenceItems.Item("c:\\nuget\\Microsoft.AspNetCore.Mvc.razor.dll");
        var afterChangeContentItems = new ItemCollection(ManagedProjectSystemSchema.ContentItem.SchemaName);
        _contentItems.Item("Index.cshtml", new Dictionary<string, string>()
        {
            [ItemReference.FullPathPropertyName] = "C:\\Path\\Index.cshtml",
        });

        var initialChanges = new TestProjectChangeDescription[]
        {
             _referenceItems.ToChange(),
             _contentItems.ToChange(),
        };
        var changes = new TestProjectChangeDescription[]
        {
             _referenceItems.ToChange(),
             afterChangeContentItems.ToChange(_contentItems.ToSnapshot()),
        };

        var services = new TestProjectSystemServices("C:\\Path\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager)
        {
            AssemblyVersion = new Version(2, 0),
        };

        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        // Act - 1
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(initialChanges)));

        // Assert - 1
        var snapshot = Assert.Single(_projectManager.GetProjects());
        Assert.Equal("C:\\Path\\Test.csproj", snapshot.FilePath);
        Assert.Same(FallbackRazorConfiguration.MVC_2_0, snapshot.Configuration);
        var filePath = Assert.Single(snapshot.DocumentFilePaths);
        Assert.Equal("C:\\Path\\Index.cshtml", filePath);

        // Act - 2
        host.AssemblyVersion = new Version(1, 0);
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert - 2
        snapshot = Assert.Single(_projectManager.GetProjects());
        Assert.Equal("C:\\Path\\Test.csproj", snapshot.FilePath);
        Assert.Same(FallbackRazorConfiguration.MVC_1_0, snapshot.Configuration);
        Assert.Empty(snapshot.DocumentFilePaths);

        await Task.Run(async () => await host.DisposeAsync());
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task OnProjectChanged_VersionRemoved_DeinitializesProject()
    {
        // Arrange
        _referenceItems.Item("c:\\nuget\\Microsoft.AspNetCore.Mvc.razor.dll");

        var changes = new TestProjectChangeDescription[]
        {
             _referenceItems.ToChange(),
        };

        var services = new TestProjectSystemServices("Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager)
        {
            AssemblyVersion = new Version(2, 0),
        };

        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        // Act - 1
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert - 1
        var snapshot = Assert.Single(_projectManager.GetProjects());
        Assert.Equal("Test.csproj", snapshot.FilePath);
        Assert.Same(FallbackRazorConfiguration.MVC_2_0, snapshot.Configuration);

        // Act - 2
        host.AssemblyVersion = null;
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert - 2
        Assert.Empty(_projectManager.GetProjects());

        await Task.Run(async () => await host.DisposeAsync());
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task OnProjectChanged_AfterDispose_IgnoresUpdate()
    {
        // Arrange
        _referenceItems.Item("c:\\nuget\\Microsoft.AspNetCore.Mvc.razor.dll");
        _contentItems.Item("Index.cshtml", new Dictionary<string, string>()
        {
            [ItemReference.FullPathPropertyName] = "C:\\Path\\Index.cshtml",
        });

        var changes = new TestProjectChangeDescription[]
        {
             _referenceItems.ToChange(),
             _contentItems.ToChange(),
        };

        var services = new TestProjectSystemServices("C:\\Path\\Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager)
        {
            AssemblyVersion = new Version(2, 0),
        };

        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        // Act - 1
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert - 1
        var snapshot = Assert.Single(_projectManager.GetProjects());
        Assert.Equal("C:\\Path\\Test.csproj", snapshot.FilePath);
        Assert.Same(FallbackRazorConfiguration.MVC_2_0, snapshot.Configuration);
        var filePath = Assert.Single(snapshot.DocumentFilePaths);
        Assert.Equal("C:\\Path\\Index.cshtml", filePath);

        // Act - 2
        await Task.Run(async () => await host.DisposeAsync());

        // Assert - 2
        Assert.Empty(_projectManager.GetProjects());

        // Act - 3
        host.AssemblyVersion = new Version(1, 1);
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert - 3
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task OnProjectRenamed_RemovesHostProject_CopiesConfiguration()
    {
        // Arrange
        _referenceItems.Item("c:\\nuget\\Microsoft.AspNetCore.Mvc.razor.dll");

        var changes = new TestProjectChangeDescription[]
        {
             _referenceItems.ToChange(),
        };

        var services = new TestProjectSystemServices("Test.csproj");

        var host = new TestFallbackRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager)
        {
            AssemblyVersion = new Version(2, 0), // Mock for reading the assembly's version
        };

        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        // Act - 1
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert - 1
        var snapshot = Assert.Single(_projectManager.GetProjects());
        Assert.Equal("Test.csproj", snapshot.FilePath);
        Assert.Same(FallbackRazorConfiguration.MVC_2_0, snapshot.Configuration);

        // Act - 2
        services.UnconfiguredProject.FullPath = "Test2.csproj";
        await Task.Run(async () => await host.OnProjectRenamingAsync("Test.csproj", "Test2.csproj"));

        // Assert - 1
        snapshot = Assert.Single(_projectManager.GetProjects());
        Assert.Equal("Test2.csproj", snapshot.FilePath);
        Assert.Same(FallbackRazorConfiguration.MVC_2_0, snapshot.Configuration);

        await Task.Run(async () => await host.DisposeAsync());
        Assert.Empty(_projectManager.GetProjects());
    }

    private class TestFallbackRazorProjectHost : FallbackWindowsRazorProjectHost
    {
        internal TestFallbackRazorProjectHost(
            IUnconfiguredProjectCommonServices commonServices,
            Workspace workspace,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            ProjectConfigurationFilePathStore projectConfigurationFilePathStore,
            ProjectSnapshotManagerBase projectManager)
            : base(commonServices, workspace, projectSnapshotManagerDispatcher, projectConfigurationFilePathStore, projectManager)
        {
            base.SkipIntermediateOutputPathExistCheck_TestOnly = true;
        }

        public Version AssemblyVersion { get; set; }

        protected override Version GetAssemblyVersion(string filePath)
        {
            return AssemblyVersion;
        }

        protected override bool TryGetIntermediateOutputPath(IImmutableDictionary<string, IProjectRuleSnapshot> state, [NotNullWhen(true)] out string path)
        {
            path = "obj";
            return true;
        }
    }

    private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
    {
        public TestProjectSnapshotManager(Workspace workspace, ProjectSnapshotManagerDispatcher dispatcher)
            : base(Mock.Of<IErrorReporter>(MockBehavior.Strict), Array.Empty<IProjectSnapshotChangeTrigger>(), workspace, dispatcher)
        {
        }
    }
}
