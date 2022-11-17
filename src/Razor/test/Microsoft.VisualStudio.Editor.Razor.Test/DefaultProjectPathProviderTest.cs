﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.VisualStudio.Editor.Razor;

public class DefaultProjectPathProviderTest : TestBase
{
    public DefaultProjectPathProviderTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public void TryGetProjectPath_NullLiveShareProjectPathProvider_UsesProjectService()
    {
        // Arrange
        var expectedProjectPath = "/my/project/path.csproj";
        var projectService = new Mock<TextBufferProjectService>(MockBehavior.Strict);
        projectService.Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Returns(new object());
        projectService.Setup(service => service.GetProjectPath(It.IsAny<object>()))
            .Returns(expectedProjectPath);
        var projectPathProvider = new DefaultProjectPathProvider(projectService.Object, liveShareProjectPathProvider: null);
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
        var liveShareProjectPathProvider = new Mock<LiveShareProjectPathProvider>(MockBehavior.Strict);
        var liveShareProjectPath = "/path/from/liveshare.csproj";
        liveShareProjectPathProvider.Setup(provider => provider.TryGetProjectPath(It.IsAny<ITextBuffer>(), out liveShareProjectPath))
            .Returns(true);
        var projectService = new Mock<TextBufferProjectService>(MockBehavior.Strict);
        projectService.Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Throws<XunitException>();
        var projectPathProvider = new DefaultProjectPathProvider(projectService.Object, liveShareProjectPathProvider.Object);
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
        var projectService = new Mock<TextBufferProjectService>(MockBehavior.Strict);
        projectService.Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Returns(value: null);
        var liveShareProjectPathProvider = new Mock<LiveShareProjectPathProvider>(MockBehavior.Strict);
        liveShareProjectPathProvider.Setup(p => p.TryGetProjectPath(It.IsAny<ITextBuffer>(), out It.Ref<string>.IsAny)).Returns(false);
        var projectPathProvider = new DefaultProjectPathProvider(projectService.Object, liveShareProjectPathProvider.Object);
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
        var projectService = new Mock<TextBufferProjectService>(MockBehavior.Strict);
        projectService.Setup(service => service.GetHostProject(It.IsAny<ITextBuffer>()))
            .Returns(new object());
        projectService.Setup(service => service.GetProjectPath(It.IsAny<object>()))
            .Returns(expectedProjectPath);
        var liveShareProjectPathProvider = new Mock<LiveShareProjectPathProvider>(MockBehavior.Strict);
        liveShareProjectPathProvider.Setup(p => p.TryGetProjectPath(It.IsAny<ITextBuffer>(), out It.Ref<string>.IsAny)).Returns(false);
        var projectPathProvider = new DefaultProjectPathProvider(projectService.Object, liveShareProjectPathProvider.Object);
        var textBuffer = Mock.Of<ITextBuffer>(MockBehavior.Strict);

        // Act
        var result = projectPathProvider.TryGetProjectPath(textBuffer, out var filePath);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedProjectPath, filePath);
    }
}
