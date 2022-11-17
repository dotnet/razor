﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class RemoteRazorProjectFileSystemTest : TestBase
{
    public RemoteRazorProjectFileSystemTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public void GetItem_RootlessFilePath()
    {
        // Arrange
        var fileSystem = new RemoteRazorProjectFileSystem("C:/path/to");
        var documentFilePath = "file.cshtml";

        // Act
        var item = fileSystem.GetItem(documentFilePath, fileKind: null);

        // Assert
        Assert.Equal(documentFilePath, item.FilePath);
        Assert.Equal("C:/path/to/file.cshtml", item.PhysicalPath);
    }

    [Fact]
    public void GetItem_RootedFilePath_BelongsToProject()
    {
        // Arrange
        var fileSystem = new RemoteRazorProjectFileSystem("C:/path/to");
        var documentFilePath = "C:/path/to/file.cshtml";

        // Act
        var item = fileSystem.GetItem(documentFilePath, fileKind: null);

        // Assert
        Assert.Equal("file.cshtml", item.FilePath);
        Assert.Equal(documentFilePath, item.PhysicalPath);
    }

    [Fact]
    public void GetItem_RootedFilePath_DoesNotBelongToProject()
    {
        // Arrange
        var fileSystem = new RemoteRazorProjectFileSystem("C:/path/to");
        var documentFilePath = "C:/otherpath/to/file.cshtml";

        // Act
        var item = fileSystem.GetItem(documentFilePath, fileKind: null);

        // Assert
        Assert.Equal(documentFilePath, item.FilePath);
        Assert.Equal(documentFilePath, item.PhysicalPath);
    }
}
