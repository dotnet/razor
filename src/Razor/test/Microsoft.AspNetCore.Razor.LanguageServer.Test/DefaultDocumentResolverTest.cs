﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class DocumentDocumentResolverTest : LanguageServerTestBase
{
    public DocumentDocumentResolverTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public void TryResolveDocument_AsksPotentialParentProjectForDocumentItsTracking_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var expectedDocument = Mock.Of<DocumentSnapshot>(MockBehavior.Strict);
        var project = Mock.Of<ProjectSnapshot>(shim => shim.GetDocument(normalizedFilePath) == expectedDocument, MockBehavior.Strict);
        var projectResolver = Mock.Of<ProjectResolver>(resolver => resolver.TryResolveProject(normalizedFilePath, out project, true) == true, MockBehavior.Strict);
        var documentResolver = new DefaultDocumentResolver(LegacyDispatcher, projectResolver);

        // Act
        var result = documentResolver.TryResolveDocument(documentFilePath, out var document);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedDocument, document);
    }

    [Fact]
    public void TryResolveDocument_AsksMiscellaneousProjectForDocumentItIsTracking_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var expectedDocument = Mock.Of<DocumentSnapshot>(MockBehavior.Strict);
        var project = Mock.Of<ProjectSnapshot>(shim => shim.GetDocument(normalizedFilePath) == expectedDocument, MockBehavior.Strict);
        var projectResolver = Mock.Of<ProjectResolver>(resolver => resolver.TryResolveProject(normalizedFilePath, out project, true) == true, MockBehavior.Strict);
        var documentResolver = new DefaultDocumentResolver(LegacyDispatcher, projectResolver);

        // Act
        var result = documentResolver.TryResolveDocument(documentFilePath, out var document);

        // Assert
        Assert.True(result);
        Assert.Same(expectedDocument, document);
    }

    [Fact]
    public void TryResolveDocument_AsksPotentialParentProjectForDocumentItsNotTrackingAndMiscellaneousProjectIsNotTrackingEither_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var project = Mock.Of<ProjectSnapshot>(shim => shim.DocumentFilePaths == Array.Empty<string>(), MockBehavior.Strict);
        var miscProject = Mock.Of<ProjectSnapshot>(shim => shim.DocumentFilePaths == Array.Empty<string>(), MockBehavior.Strict);
        ProjectSnapshot noProject = null;
        var projectResolver = Mock.Of<ProjectResolver>(resolver =>
            resolver.TryResolveProject(normalizedFilePath, out noProject, true) == false, MockBehavior.Strict);
        var documentResolver = new DefaultDocumentResolver(LegacyDispatcher, projectResolver);

        // Act
        var result = documentResolver.TryResolveDocument(documentFilePath, out var document);

        // Assert
        Assert.False(result);
        Assert.Null(document);
    }

    [Fact]
    public void TryResolveDocument_AsksPotentialParentProjectForDocumentItsNotTrackingButMiscellaneousProjectIs_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var expectedDocument = Mock.Of<DocumentSnapshot>(MockBehavior.Strict);
        var project = Mock.Of<ProjectSnapshot>(shim => shim.DocumentFilePaths == Array.Empty<string>(), MockBehavior.Strict);
        var miscProject = Mock.Of<ProjectSnapshot>(shim => shim.GetDocument(normalizedFilePath) == expectedDocument, MockBehavior.Strict);
        var projectResolver = Mock.Of<ProjectResolver>(resolver =>
            resolver.TryResolveProject(normalizedFilePath, out miscProject, true) == true, MockBehavior.Strict);
        var documentResolver = new DefaultDocumentResolver(LegacyDispatcher, projectResolver);

        // Act
        var result = documentResolver.TryResolveDocument(documentFilePath, out var document);

        // Assert
        Assert.True(result);
        Assert.Same(expectedDocument, document);
    }
}
