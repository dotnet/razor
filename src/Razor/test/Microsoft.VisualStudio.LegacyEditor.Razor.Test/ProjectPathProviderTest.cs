// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LegacyEditor.Razor;

public class ProjectPathProviderTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [Fact]
    public void TryGetProjectPath_NullLiveShareProjectPathProvider_UsesProjectService()
    {
        // Arrange
        var expectedProjectPath = "/my/project/path.csproj";
        var projectServiceMock = new StrictMock<ITextBufferProjectService>();
        projectServiceMock
            .Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Returns(new object());
        projectServiceMock
            .Setup(service => service.GetProjectPath(It.IsAny<object>()))
            .Returns(expectedProjectPath);
        var projectPathProvider = new ProjectPathProvider(projectServiceMock.Object, liveShareProjectPathProvider: null);
        var textBuffer = StrictMock.Of<ITextBuffer>();

        // Act
        var result = projectPathProvider.TryGetProjectPath(textBuffer, out var filePath);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedProjectPath, filePath);
    }

    [Fact]
    public void TryGetProjectPath_PrioritizesLiveShareProjectPathProvider()
    {
        // Arrange
        var liveShareProjectPathProviderMock = new StrictMock<ILiveShareProjectPathProvider>();
        var liveShareProjectPath = "/path/from/liveshare.csproj";
        liveShareProjectPathProviderMock
            .Setup(provider => provider.TryGetProjectPath(It.IsAny<ITextBuffer>(), out liveShareProjectPath))
            .Returns(true);
        var projectServiceMock = new StrictMock<ITextBufferProjectService>();
        projectServiceMock
            .Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Throws<Exception>();
        var projectPathProvider = new ProjectPathProvider(projectServiceMock.Object, liveShareProjectPathProviderMock.Object);
        var textBuffer = StrictMock.Of<ITextBuffer>();

        // Act
        var result = projectPathProvider.TryGetProjectPath(textBuffer, out var filePath);

        // Assert
        Assert.True(result);
        Assert.Equal(liveShareProjectPath, filePath);
    }

    [Fact]
    public void TryGetProjectPath_ReturnsFalseIfNoProject()
    {
        // Arrange
        var projectServiceMock = new StrictMock<ITextBufferProjectService>();
        projectServiceMock
            .Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Returns(value: null);
        var liveShareProjectPathProviderMock = new StrictMock<ILiveShareProjectPathProvider>();
        liveShareProjectPathProviderMock
            .Setup(p => p.TryGetProjectPath(It.IsAny<ITextBuffer>(), out It.Ref<string?>.IsAny))
            .Returns(false);
        var projectPathProvider = new ProjectPathProvider(projectServiceMock.Object, liveShareProjectPathProviderMock.Object);
        var textBuffer = StrictMock.Of<ITextBuffer>();

        // Act
        var result = projectPathProvider.TryGetProjectPath(textBuffer, out var filePath);

        // Assert
        Assert.False(result);
        Assert.Null(filePath);
    }

    [Fact]
    public void TryGetProjectPath_ReturnsTrueIfProject()
    {
        // Arrange
        var expectedProjectPath = "/my/project/path.csproj";
        var projectServiceMock = new StrictMock<ITextBufferProjectService>();
        projectServiceMock
            .Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Returns(new object());
        projectServiceMock
            .Setup(service => service.GetProjectPath(It.IsAny<object>()))
            .Returns(expectedProjectPath);
        var liveShareProjectPathProviderMock = new StrictMock<ILiveShareProjectPathProvider>();
        liveShareProjectPathProviderMock
            .Setup(p => p.TryGetProjectPath(It.IsAny<ITextBuffer>(), out It.Ref<string?>.IsAny))
            .Returns(false);
        var projectPathProvider = new ProjectPathProvider(projectServiceMock.Object, liveShareProjectPathProviderMock.Object);
        var textBuffer = StrictMock.Of<ITextBuffer>();

        // Act
        var result = projectPathProvider.TryGetProjectPath(textBuffer, out var filePath);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedProjectPath, filePath);
    }
}
