﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

public class CSharpVirtualDocumentFactoryTest : VisualStudioTestBase
{
    private readonly ITextBuffer _nonRazorLSPBuffer;
    private readonly ITextBuffer _razorLSPBuffer;
    private readonly IContentTypeRegistryService _contentTypeRegistryService;
    private readonly ITextBufferFactoryService _textBufferFactoryService;
    private readonly ITextDocumentFactoryService _textDocumentFactoryService;
    private readonly FilePathService _filePathService;

    public CSharpVirtualDocumentFactoryTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _contentTypeRegistryService = StrictMock.Of<IContentTypeRegistryService>(x =>
            x.GetContentType(RazorLSPConstants.CSharpContentTypeName) == VsMocks.ContentTypes.CSharp);

        var textBufferFactoryServiceMock = new StrictMock<ITextBufferFactoryService>();
        textBufferFactoryServiceMock
            .Setup(x => x.CreateTextBuffer())
            .Returns(() =>
            {
                var buffer = VsMocks.CreateTextBuffer();
                var mock = Mock.Get(buffer);

                mock.SetupGet(x => x.CurrentSnapshot)
                    .Returns(StrictMock.Of<ITextSnapshot>());
                mock.Setup(b => b.ChangeContentType(It.IsAny<IContentType>(), It.IsAny<object>()))
                    .Verifiable();

                return buffer;
            });

        _textBufferFactoryService = textBufferFactoryServiceMock.Object;

        var textDocumentFactoryServiceMock = new StrictMock<ITextDocumentFactoryService>();
        textDocumentFactoryServiceMock
            .Setup(x => x.CreateTextDocument(It.IsAny<ITextBuffer>(), It.IsAny<string>()))
            .Returns((ITextDocument)null!);

        _textDocumentFactoryService = textDocumentFactoryServiceMock.Object;

        _razorLSPBuffer = VsMocks.CreateTextBuffer(VsMocks.ContentTypes.RazorLSP);
        _nonRazorLSPBuffer = VsMocks.CreateTextBuffer(VsMocks.ContentTypes.NonRazor);

        _filePathService = new FilePathService(TestLanguageServerFeatureOptions.Instance);
    }

    [Fact]
    public void TryCreateMultipleFor_NonRazorLSPBuffer_ReturnsFalse()
    {
        // Arrange
        var uri = new Uri("C:/path/to/file.razor");
        var uriProvider = StrictMock.Of<FileUriProvider>(x =>
            x.GetOrCreate(It.IsAny<ITextBuffer>()) == uri);

        var factory = new CSharpVirtualDocumentFactory(
            _contentTypeRegistryService,
            _textBufferFactoryService,
            _textDocumentFactoryService,
            uriProvider,
            _filePathService,
            StrictMock.Of<IProjectSnapshotManagerAccessor>(),
            TestLanguageServerFeatureOptions.Instance,
            LoggerFactory,
            telemetryReporter: null!);

        // Act
        var result = factory.TryCreateMultipleFor(_nonRazorLSPBuffer, out var virtualDocuments);

        // Assert
        Assert.False(result);
        Assert.Null(virtualDocuments);
    }

    [Fact]
    public void TryCreateMultipleFor_NoProjectSnapshotManager_ReturnsFalse()
    {
        // Arrange
        var uri = new Uri("C:/path/to/file.razor");
        var uriProvider = StrictMock.Of<FileUriProvider>(x =>
            x.GetOrCreate(It.IsAny<ITextBuffer>()) == uri);

        var projectManagerAccessorMock = new StrictMock<IProjectSnapshotManagerAccessor>();

        ProjectSnapshotManagerBase? instance = null;
        projectManagerAccessorMock
            .Setup(x => x.TryGetInstance(out instance))
            .Returns(false);

        var factory = new CSharpVirtualDocumentFactory(
            _contentTypeRegistryService,
            _textBufferFactoryService,
            _textDocumentFactoryService,
            uriProvider,
            _filePathService,
            projectManagerAccessorMock.Object,
            TestLanguageServerFeatureOptions.Instance,
            LoggerFactory,
            telemetryReporter: null!);

        // Act
        var result = factory.TryCreateMultipleFor(_nonRazorLSPBuffer, out var virtualDocuments);

        // Assert
        Assert.False(result);
        Assert.Null(virtualDocuments);
    }

    [Fact]
    public async Task TryCreateMultipleFor_RazorLSPBuffer_ReturnsCSharpVirtualDocumentAndTrue()
    {
        // Arrange
        var uri = new Uri("C:/path/to/file.razor");
        var uriProvider = StrictMock.Of<FileUriProvider>(x =>
            x.GetOrCreate(_razorLSPBuffer) == uri);
        Mock.Get(uriProvider)
            .Setup(x => x.AddOrUpdate(It.IsAny<ITextBuffer>(), It.IsAny<Uri>()))
            .Verifiable();

        var projectManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);

        await RunOnDispatcherAsync(() =>
        {
            var project = projectManager.CreateAndAddProject(@"C:\path\to\project.csproj");
            projectManager.CreateAndAddDocument(project, @"C:\path\to\file.razor");
        });

        var projectManagerAccessor = StrictMock.Of<IProjectSnapshotManagerAccessor>(a =>
            a.Instance == projectManager);

        var factory = new CSharpVirtualDocumentFactory(
            _contentTypeRegistryService,
            _textBufferFactoryService,
            _textDocumentFactoryService,
            uriProvider,
            _filePathService,
            projectManagerAccessor,
            TestLanguageServerFeatureOptions.Instance,
            LoggerFactory,
            telemetryReporter: null!);

        // Act
        Assert.True(factory.TryCreateMultipleFor(_razorLSPBuffer, out var virtualDocuments));

        // Assert
        using var virtualDocument = Assert.Single(virtualDocuments);
        Assert.EndsWith(TestLanguageServerFeatureOptions.Instance.CSharpVirtualDocumentSuffix, virtualDocument.Uri.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryCreateMultipleFor_RazorLSPBuffer_ReturnsMultipleCSharpVirtualDocumentsAndTrue()
    {
        // Arrange
        var uri = new Uri("C:/path/to/file.razor");
        var uriProvider = StrictMock.Of<FileUriProvider>(x =>
            x.GetOrCreate(_razorLSPBuffer) == uri);
        Mock.Get(uriProvider)
            .Setup(x => x.AddOrUpdate(It.IsAny<ITextBuffer>(), It.IsAny<Uri>()))
            .Verifiable();

        var projectManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);

        await RunOnDispatcherAsync(() =>
        {
            var project1 = TestProjectSnapshot.Create(
                @"C:\path\to\project1.csproj",
                @"C:\path\to\obj1",
                documentFilePaths: [],
                RazorConfiguration.Default,
                projectWorkspaceState: null);
            projectManager.ProjectAdded(project1.HostProject);
            projectManager.CreateAndAddDocument(project1, @"C:\path\to\file.razor");

            var project2 = TestProjectSnapshot.Create(
                @"C:\path\to\project2.csproj",
                @"C:\path\to\obj2",
                documentFilePaths: [],
                RazorConfiguration.Default,
                projectWorkspaceState: null);
            projectManager.ProjectAdded(project2.HostProject);
            projectManager.CreateAndAddDocument(project2, @"C:\path\to\file.razor");
        });

        var projectManagerAccessorMock = new StrictMock<IProjectSnapshotManagerAccessor>();
        projectManagerAccessorMock
            .SetupGet(x => x.Instance)
            .Returns(projectManager);

        ProjectSnapshotManagerBase? instance = projectManager;
        projectManagerAccessorMock
            .Setup(x => x.TryGetInstance(out instance))
            .Returns(true);

        var projectManagerAccessor = projectManagerAccessorMock.Object;

        var languageServerFeatureOptions = new TestLanguageServerFeatureOptions(includeProjectKeyInGeneratedFilePath: true);
        var filePathService = new FilePathService(languageServerFeatureOptions);
        var factory = new CSharpVirtualDocumentFactory(
            _contentTypeRegistryService,
            _textBufferFactoryService,
            _textDocumentFactoryService,
            uriProvider,
            filePathService,
            projectManagerAccessor,
            languageServerFeatureOptions,
            LoggerFactory,
            telemetryReporter: null!);

        // Act
        Assert.True(factory.TryCreateMultipleFor(_razorLSPBuffer, out var virtualDocuments));

        // Assert
        Assert.Equal(2, virtualDocuments.Length);
        Assert.Collection(virtualDocuments,
            item => Assert.Equal("C:/path/to/file.razor.ooJmNcWMKXNlf5MK.ide.g.cs", item.Uri.OriginalString),
            item => Assert.Equal("C:/path/to/file.razor.jGYrFHvWEciJi85y.ide.g.cs", item.Uri.OriginalString));
    }
}
