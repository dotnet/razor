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
        var projectService = new Mock<ITextBufferProjectService>(MockBehavior.Strict);
        projectService
            .Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Returns(new object());
        projectService
            .Setup(service => service.GetProjectPath(It.IsAny<object>()))
            .Returns(expectedProjectPath);
        var projectPathProvider = new ProjectPathProvider(projectService.Object, liveShareProjectPathProvider: null);
        var textBuffer = Mock.Of<ITextBuffer>(MockBehavior.Strict);

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
        var liveShareProjectPathProvider = new Mock<ILiveShareProjectPathProvider>(MockBehavior.Strict);
        var liveShareProjectPath = "/path/from/liveshare.csproj";
        liveShareProjectPathProvider
            .Setup(provider => provider.TryGetProjectPath(It.IsAny<ITextBuffer>(), out liveShareProjectPath))
            .Returns(true);
        var projectService = new Mock<ITextBufferProjectService>(MockBehavior.Strict);
        projectService
            .Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Throws<Exception>();
        var projectPathProvider = new ProjectPathProvider(projectService.Object, liveShareProjectPathProvider.Object);
        var textBuffer = Mock.Of<ITextBuffer>(MockBehavior.Strict);

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
        var projectService = new Mock<ITextBufferProjectService>(MockBehavior.Strict);
        projectService
            .Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Returns(value: null);
        var liveShareProjectPathProvider = new Mock<ILiveShareProjectPathProvider>(MockBehavior.Strict);
        liveShareProjectPathProvider
            .Setup(p => p.TryGetProjectPath(It.IsAny<ITextBuffer>(), out It.Ref<string?>.IsAny))
            .Returns(false);
        var projectPathProvider = new ProjectPathProvider(projectService.Object, liveShareProjectPathProvider.Object);
        var textBuffer = Mock.Of<ITextBuffer>(MockBehavior.Strict);

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
        var projectService = new Mock<ITextBufferProjectService>(MockBehavior.Strict);
        projectService
            .Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Returns(new object());
        projectService
            .Setup(service => service.GetProjectPath(It.IsAny<object>()))
            .Returns(expectedProjectPath);
        var liveShareProjectPathProvider = new Mock<ILiveShareProjectPathProvider>(MockBehavior.Strict);
        liveShareProjectPathProvider
            .Setup(p => p.TryGetProjectPath(It.IsAny<ITextBuffer>(), out It.Ref<string?>.IsAny))
            .Returns(false);
        var projectPathProvider = new ProjectPathProvider(projectService.Object, liveShareProjectPathProvider.Object);
        var textBuffer = Mock.Of<ITextBuffer>(MockBehavior.Strict);

        // Act
        var result = projectPathProvider.TryGetProjectPath(textBuffer, out var filePath);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedProjectPath, filePath);
    }
}
