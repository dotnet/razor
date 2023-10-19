﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class RazorSourceDocumentTest
{
    [Fact]
    public void ReadFrom()
    {
        // Arrange
        var content = TestRazorSourceDocument.CreateStreamContent();

        // Act
        var document = RazorSourceDocument.ReadFrom(content, "file.cshtml");

        // Assert
        Assert.Equal("file.cshtml", document.FilePath);
        Assert.Same(Encoding.UTF8, document.Text.Encoding);
    }

    [Fact]
    public void ReadFrom_WithEncoding()
    {
        // Arrange
        var content = TestRazorSourceDocument.CreateStreamContent(encoding: Encoding.UTF32);

        // Act
        var document = RazorSourceDocument.ReadFrom(content, "file.cshtml", Encoding.UTF32);

        // Assert
        Assert.Equal("file.cshtml", document.FilePath);
        Assert.Same(Encoding.UTF32, document.Text.Encoding);
    }

    [Fact]
    public void ReadFrom_WithProperties()
    {
        // Arrange
        var content = TestRazorSourceDocument.CreateStreamContent(encoding: Encoding.UTF32);
        var properties = RazorSourceDocumentProperties.Create("c:\\myapp\\filePath.cshtml", "filePath.cshtml");

        // Act
        var document = RazorSourceDocument.ReadFrom(content, Encoding.UTF32, properties);

        // Assert
        Assert.Equal("c:\\myapp\\filePath.cshtml", document.FilePath);
        Assert.Equal("filePath.cshtml", document.RelativePath);
        Assert.Same(Encoding.UTF32, document.Text.Encoding);
    }

    [Fact]
    public void ReadFrom_EmptyStream_WithEncoding()
    {
        // Arrange
        var content = TestRazorSourceDocument.CreateStreamContent(content: string.Empty, encoding: Encoding.UTF32);

        // Act
        var document = RazorSourceDocument.ReadFrom(content, "file.cshtml", Encoding.UTF32);

        // Assert
        Assert.Equal("file.cshtml", document.FilePath);
        Assert.Same(Encoding.UTF32, document.Text.Encoding);
    }

    [Fact]
    public void ReadFrom_ProjectItem()
    {
        // Arrange
        var projectItem = new TestRazorProjectItem("filePath.cshtml", "c:\\myapp\\filePath.cshtml", "filePath.cshtml", "c:\\myapp\\");

        // Act
        var document = RazorSourceDocument.ReadFrom(projectItem);

        // Assert
        Assert.Equal("c:\\myapp\\filePath.cshtml", document.FilePath);
        Assert.Equal("filePath.cshtml", document.RelativePath);
        Assert.Equal(projectItem.Content, ReadContent(document));
    }

    [Fact]
    public void ReadFrom_ProjectItem_NoRelativePath()
    {
        // Arrange
        var projectItem = new TestRazorProjectItem("filePath.cshtml", "c:\\myapp\\filePath.cshtml", basePath: "c:\\myapp\\");

        // Act
        var document = RazorSourceDocument.ReadFrom(projectItem);

        // Assert
        Assert.Equal("c:\\myapp\\filePath.cshtml", document.FilePath);
        Assert.Equal("filePath.cshtml", document.RelativePath);
        Assert.Equal(projectItem.Content, ReadContent(document));
    }

    [Fact]
    public void ReadFrom_ProjectItem_FallbackToRelativePath()
    {
        // Arrange
        var projectItem = new TestRazorProjectItem("filePath.cshtml", relativePhysicalPath: "filePath.cshtml", basePath: "c:\\myapp\\");

        // Act
        var document = RazorSourceDocument.ReadFrom(projectItem);

        // Assert
        Assert.Equal("filePath.cshtml", document.FilePath);
        Assert.Equal("filePath.cshtml", document.RelativePath);
        Assert.Equal(projectItem.Content, ReadContent(document));
    }

    [Fact]
    public void ReadFrom_ProjectItem_FallbackToFileName()
    {
        // Arrange
        var projectItem = new TestRazorProjectItem("filePath.cshtml", basePath: "c:\\myapp\\");

        // Act
        var document = RazorSourceDocument.ReadFrom(projectItem);

        // Assert
        Assert.Equal("filePath.cshtml", document.FilePath);
        Assert.Equal("filePath.cshtml", document.RelativePath);
        Assert.Equal(projectItem.Content, ReadContent(document));
    }

    [Fact]
    public void Create_WithoutEncoding()
    {
        // Arrange
        var content = "Hello world";
        var fileName = "some-file-name";

        // Act
        var document = RazorSourceDocument.Create(content, fileName);

        // Assert
        Assert.Equal(fileName, document.FilePath);
        Assert.Equal(content, ReadContent(document));
        Assert.Same(Encoding.UTF8, document.Text.Encoding);
    }

    [Fact]
    public void Create_WithEncoding()
    {
        // Arrange
        var content = "Hello world";
        var fileName = "some-file-name";
        var encoding = Encoding.UTF32;

        // Act
        var document = RazorSourceDocument.Create(content, fileName, encoding);

        // Assert
        Assert.Equal(fileName, document.FilePath);
        Assert.Equal(content, ReadContent(document));
        Assert.Same(encoding, document.Text.Encoding);
    }

    [Fact]
    public void Create_WithProperties()
    {
        // Arrange
        var content = "Hello world";
        var properties = RazorSourceDocumentProperties.Create("c:\\myapp\\filePath.cshtml", "filePath.cshtml");

        // Act
        var document = RazorSourceDocument.Create(content, Encoding.UTF32, properties);

        // Assert
        Assert.Equal("c:\\myapp\\filePath.cshtml", document.FilePath);
        Assert.Equal("filePath.cshtml", document.RelativePath);
        Assert.Equal(content, ReadContent(document));
        Assert.Same(Encoding.UTF32, document.Text.Encoding);
    }

    [Fact]
    public void ReadFrom_WithProjectItem_FallbackToFilePath_WhenRelativePhysicalPathIsNull()
    {
        // Arrange
        var filePath = "filePath.cshtml";
        var projectItem = new TestRazorProjectItem(filePath, relativePhysicalPath: null);

        // Act
        var document = RazorSourceDocument.ReadFrom(projectItem);

        // Assert
        Assert.Equal(filePath, document.FilePath);
        Assert.Equal(filePath, document.RelativePath);
    }

    [Fact]
    public void ReadFrom_WithProjectItem_UsesRelativePhysicalPath()
    {
        // Arrange
        var filePath = "filePath.cshtml";
        var relativePhysicalPath = "relative-path.cshtml";
        var projectItem = new TestRazorProjectItem(filePath, relativePhysicalPath: relativePhysicalPath);

        // Act
        var document = RazorSourceDocument.ReadFrom(projectItem);

        // Assert
        Assert.Equal(relativePhysicalPath, document.FilePath);
        Assert.Equal(relativePhysicalPath, document.RelativePath);
    }

    private static string ReadContent(RazorSourceDocument razorSourceDocument) => razorSourceDocument.Text.ToString();
}
