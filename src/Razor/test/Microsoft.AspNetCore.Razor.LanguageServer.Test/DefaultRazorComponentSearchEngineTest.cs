// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

public class DefaultRazorComponentSearchEngineTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    private const string ProjectFilePath1 = "c:/First/First.csproj";
    private const string ProjectFilePath2 = "c:/Second/Second.csproj";

    private const string IntermediateOutputPath1 = "c:/First/obj";
    private const string IntermediateOutputPath2 = "c:/Second/obj";

    private const string RootNamespace1 = "First.Components";
    private const string RootNamespace2 = "Second.Components";

    private const string ComponentFilePath1 = "c:/First/Component1.razor";
    private const string ComponentFilePath2 = "c:/First/Component2.razor";
    private const string ComponentFilePath3 = "c:/Second/Component3.razor";

    private TestProjectSnapshotManager _projectManager;

    protected override async Task InitializeAsync()
    {
        _projectManager = CreateProjectSnapshotManager();

        var snapshotResolver = new SnapshotResolver(_projectManager, LoggerFactory);
        var documentVersionCache = new DocumentVersionCache(_projectManager);

        var remoteTextLoaderFactoryMock = new StrictMock<RemoteTextLoaderFactory>();
        remoteTextLoaderFactoryMock
            .Setup(x => x.Create(It.IsAny<string>()))
            .Returns((string filePath) =>
            {
                var textLoaderMock = new StrictMock<TextLoader>();
                textLoaderMock
                    .Setup(x => x.LoadTextAndVersionAsync(It.IsAny<LoadTextOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TextAndVersion.Create(SourceText.From(""), VersionStamp.Create(), filePath));

                return textLoaderMock.Object;
            });

        var projectService = new RazorProjectService(
            Dispatcher,
            remoteTextLoaderFactoryMock.Object,
            snapshotResolver,
            documentVersionCache,
            _projectManager,
            LoggerFactory);

        await RunOnDispatcherAsync(() =>
        {
            projectService.AddProject(
                ProjectFilePath1,
                IntermediateOutputPath1,
                RazorConfiguration.Default,
                RootNamespace1,
                displayName: "");

            projectService.AddDocument(ComponentFilePath1);
            projectService.UpdateDocument(ComponentFilePath1, SourceText.From(""), version: 1);

            projectService.AddDocument(ComponentFilePath2);
            projectService.UpdateDocument(ComponentFilePath2, SourceText.From("@namespace Test"), version: 1);

            projectService.AddProject(
                ProjectFilePath2,
                IntermediateOutputPath2,
                RazorConfiguration.Default,
                RootNamespace2,
                displayName: "");

            projectService.AddDocument(ComponentFilePath3);
            projectService.UpdateDocument(ComponentFilePath3, SourceText.From(""), version: 1);
        });
    }

    [Fact]
    public async Task Handle_SearchFound_GenericComponent()
    {
        // Arrange
        var tagHelperDescriptor1 = CreateRazorComponentTagHelperDescriptor("First", RootNamespace1, "Component1", typeName: "Component1<TItem>");
        var tagHelperDescriptor2 = CreateRazorComponentTagHelperDescriptor("Second", RootNamespace2, "Component3", typeName: "Component3<TItem>");
        var searchEngine = new DefaultRazorComponentSearchEngine(_projectManager, LoggerFactory);

        // Act
        var documentSnapshot1 = await searchEngine.TryLocateComponentAsync(tagHelperDescriptor1);
        var documentSnapshot2 = await searchEngine.TryLocateComponentAsync(tagHelperDescriptor2);

        // Assert
        Assert.NotNull(documentSnapshot1);
        Assert.Equal(ComponentFilePath1, documentSnapshot1.FilePath);
        Assert.NotNull(documentSnapshot2);
        Assert.Equal(ComponentFilePath3, documentSnapshot2.FilePath);
    }

    [Fact]
    public async Task Handle_SearchFound()
    {
        // Arrange
        var tagHelperDescriptor1 = CreateRazorComponentTagHelperDescriptor("First", RootNamespace1, "Component1");
        var tagHelperDescriptor2 = CreateRazorComponentTagHelperDescriptor("Second", RootNamespace2, "Component3");
        var searchEngine = new DefaultRazorComponentSearchEngine(_projectManager, LoggerFactory);

        // Act
        var documentSnapshot1 = await searchEngine.TryLocateComponentAsync(tagHelperDescriptor1);
        var documentSnapshot2 = await searchEngine.TryLocateComponentAsync(tagHelperDescriptor2);

        // Assert
        Assert.NotNull(documentSnapshot1);
        Assert.Equal(ComponentFilePath1, documentSnapshot1.FilePath);
        Assert.NotNull(documentSnapshot2);
        Assert.Equal(ComponentFilePath3, documentSnapshot2.FilePath);
    }

    [Fact]
    public async Task Handle_SearchFound_SetNamespace()
    {
        // Arrange
        var tagHelperDescriptor = CreateRazorComponentTagHelperDescriptor("First", "Test", "Component2");
        var searchEngine = new DefaultRazorComponentSearchEngine(_projectManager, LoggerFactory);

        // Act
        var documentSnapshot = await searchEngine.TryLocateComponentAsync(tagHelperDescriptor);

        // Assert
        Assert.NotNull(documentSnapshot);
        Assert.Equal(ComponentFilePath2, documentSnapshot.FilePath);
    }

    [Fact]
    public async Task Handle_SearchMissing_IncorrectAssembly()
    {
        // Arrange
        var tagHelperDescriptor = CreateRazorComponentTagHelperDescriptor("Third", RootNamespace1, "Component3");
        var searchEngine = new DefaultRazorComponentSearchEngine(_projectManager, LoggerFactory);

        // Act
        var documentSnapshot = await searchEngine.TryLocateComponentAsync(tagHelperDescriptor);

        // Assert
        Assert.Null(documentSnapshot);
    }

    [Fact]
    public async Task Handle_SearchMissing_IncorrectNamespace()
    {
        // Arrange
        var tagHelperDescriptor = CreateRazorComponentTagHelperDescriptor("First", RootNamespace1, "Component2");
        var searchEngine = new DefaultRazorComponentSearchEngine(_projectManager, LoggerFactory);

        // Act
        var documentSnapshot = await searchEngine.TryLocateComponentAsync(tagHelperDescriptor);

        // Assert
        Assert.Null(documentSnapshot);
    }

    [Fact]
    public async Task Handle_SearchMissing_IncorrectComponent()
    {
        // Arrange
        var tagHelperDescriptor = CreateRazorComponentTagHelperDescriptor("First", RootNamespace1, "Component3");
        var searchEngine = new DefaultRazorComponentSearchEngine(_projectManager, LoggerFactory);

        // Act
        var documentSnapshot = await searchEngine.TryLocateComponentAsync(tagHelperDescriptor);

        // Assert
        Assert.Null(documentSnapshot);
    }

    [Fact]
    public async Task Handle_FilePathAndAssemblyNameDifferent()
    {
        // Arrange
        var tagHelperDescriptor = CreateRazorComponentTagHelperDescriptor("AssemblyName", "Test", "Component2");
        var searchEngine = new DefaultRazorComponentSearchEngine(_projectManager, LoggerFactory);

        // Act
        var documentSnapshot = await searchEngine.TryLocateComponentAsync(tagHelperDescriptor);

        // Assert
        Assert.NotNull(documentSnapshot);
        Assert.Equal(ComponentFilePath2, documentSnapshot.FilePath);
    }

    internal static TagHelperDescriptor CreateRazorComponentTagHelperDescriptor(string assemblyName, string namespaceName, string tagName, string typeName = null)
    {
        typeName ??= tagName;
        var fullyQualifiedName = $"{namespaceName}.{typeName}";
        var builder1 = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, fullyQualifiedName, assemblyName);
        builder1.TagMatchingRule(rule => rule.TagName = tagName);
        builder1.SetMetadata(
            TypeNameIdentifier(typeName),
            TypeNamespace(namespaceName));

        return builder1.Build();
    }
}
