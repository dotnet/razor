﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Editor.Razor;

public class DefaultRazorEditorFactoryServiceTest : TestBase
{
    private readonly IContentType _razorCoreContentType;
    private readonly IContentType _nonRazorCoreContentType;

    public DefaultRazorEditorFactoryServiceTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _razorCoreContentType = Mock.Of<IContentType>(
            c => c.IsOfType(RazorLanguage.CoreContentType) == true,
            MockBehavior.Strict);

        _nonRazorCoreContentType = Mock.Of<IContentType>(
            c => c.IsOfType(It.IsAny<string>()) == false,
            MockBehavior.Strict);
    }

    [Fact]
    public void TryGetDocumentTracker_ForRazorTextBuffer_ReturnsTrue()
    {
        // Arrange
        var expectedDocumentTracker = Mock.Of<VisualStudioDocumentTracker>(MockBehavior.Strict);
        var factoryService = CreateFactoryService(expectedDocumentTracker);
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryGetDocumentTracker(textBuffer, out var documentTracker);

        // Assert
        Assert.True(result);
        Assert.Same(expectedDocumentTracker, documentTracker);
    }

    [Fact]
    public void TryGetDocumentTracker_NonRazorBuffer_ReturnsFalse()
    {
        // Arrange
        var factoryService = CreateFactoryService();
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _nonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryGetDocumentTracker(textBuffer, out var documentTracker);

        // Assert
        Assert.False(result);
        Assert.Null(documentTracker);
    }

    [Fact]
    public void TryInitializeTextBuffer_WorkspaceAccessorCanNotAccessWorkspace_ReturnsFalse()
    {
        // Arrange
        Workspace workspace = null;
        var workspaceAccessor = new Mock<VisualStudioWorkspaceAccessor>(MockBehavior.Strict);
        workspaceAccessor.Setup(provider => provider.TryGetWorkspace(It.IsAny<ITextBuffer>(), out workspace))
            .Returns(false);
        var factoryService = new DefaultRazorEditorFactoryService(workspaceAccessor.Object);
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryInitializeTextBuffer(textBuffer);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TryInitializeTextBuffer_StoresTracker_ReturnsTrue()
    {
        // Arrange
        var expectedDocumentTracker = Mock.Of<VisualStudioDocumentTracker>(MockBehavior.Strict);
        var factoryService = CreateFactoryService(expectedDocumentTracker);
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryInitializeTextBuffer(textBuffer);

        // Assert
        Assert.True(result);
        Assert.True(textBuffer.Properties.TryGetProperty(typeof(VisualStudioDocumentTracker), out VisualStudioDocumentTracker documentTracker));
        Assert.Same(expectedDocumentTracker, documentTracker);
    }

    [Fact]
    public void TryInitializeTextBuffer_OnlyStoresTrackerOnTextBufferOnce_ReturnsTrue()
    {
        // Arrange
        var factoryService = CreateFactoryService();
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);
        factoryService.TryInitializeTextBuffer(textBuffer);
        var expectedDocumentTracker = textBuffer.Properties[typeof(VisualStudioDocumentTracker)];

        // Create a second factory service so it generates a different tracker
        factoryService = CreateFactoryService();

        // Act
        var result = factoryService.TryInitializeTextBuffer(textBuffer);

        // Assert
        Assert.True(result);
        Assert.True(textBuffer.Properties.TryGetProperty(typeof(VisualStudioDocumentTracker), out VisualStudioDocumentTracker documentTracker));
        Assert.Same(expectedDocumentTracker, documentTracker);
    }

    [Fact]
    public void TryGetParser_ForRazorTextBuffer_ReturnsTrue()
    {
        // Arrange
        var expectedParser = Mock.Of<VisualStudioRazorParser>(MockBehavior.Strict);
        var factoryService = CreateFactoryService(parser: expectedParser);
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryGetParser(textBuffer, out var parser);

        // Assert
        Assert.True(result);
        Assert.Same(expectedParser, parser);
    }

    [Fact]
    public void TryGetParser_NonRazorBuffer_ReturnsFalse()
    {
        // Arrange
        var factoryService = CreateFactoryService();
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _nonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryGetParser(textBuffer, out var parser);

        // Assert
        Assert.False(result);
        Assert.Null(parser);
    }

    [Fact]
    public void TryInitializeTextBuffer_StoresParser_ReturnsTrue()
    {
        // Arrange
        var expectedParser = Mock.Of<VisualStudioRazorParser>(MockBehavior.Strict);
        var factoryService = CreateFactoryService(parser: expectedParser);
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryInitializeTextBuffer(textBuffer);

        // Assert
        Assert.True(result);
        Assert.True(textBuffer.Properties.TryGetProperty(typeof(VisualStudioRazorParser), out VisualStudioRazorParser parser));
        Assert.Same(expectedParser, parser);
    }

    [Fact]
    public void TryInitializeTextBuffer_OnlyStoresParserOnTextBufferOnce_ReturnsTrue()
    {
        // Arrange
        var factoryService = CreateFactoryService();
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);
        factoryService.TryInitializeTextBuffer(textBuffer);
        var expectedParser = textBuffer.Properties[typeof(VisualStudioRazorParser)];

        // Create a second factory service so it generates a different parser
        factoryService = CreateFactoryService();

        // Act
        var result = factoryService.TryInitializeTextBuffer(textBuffer);

        // Assert
        Assert.True(result);
        Assert.True(textBuffer.Properties.TryGetProperty(typeof(VisualStudioRazorParser), out VisualStudioRazorParser parser));
        Assert.Same(expectedParser, parser);
    }

    [Fact]
    public void TryGetSmartIndenter_ForRazorTextBuffer_ReturnsTrue()
    {
        // Arrange
        var expectedSmartIndenter = Mock.Of<BraceSmartIndenter>(MockBehavior.Strict);
        var factoryService = CreateFactoryService(smartIndenter: expectedSmartIndenter);
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryGetSmartIndenter(textBuffer, out var smartIndenter);

        // Assert
        Assert.True(result);
        Assert.Same(expectedSmartIndenter, smartIndenter);
    }

    [Fact]
    public void TryGetSmartIndenter_NonRazorBuffer_ReturnsFalse()
    {
        // Arrange
        var factoryService = CreateFactoryService();
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _nonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryGetSmartIndenter(textBuffer, out var smartIndenter);

        // Assert
        Assert.False(result);
        Assert.Null(smartIndenter);
    }

    [Fact]
    public void TryInitializeTextBuffer_StoresSmartIndenter_ReturnsTrue()
    {
        // Arrange
        var expectedSmartIndenter = Mock.Of<BraceSmartIndenter>(MockBehavior.Strict);
        var factoryService = CreateFactoryService(smartIndenter: expectedSmartIndenter);
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);

        // Act
        var result = factoryService.TryInitializeTextBuffer(textBuffer);

        // Assert
        Assert.True(result);
        Assert.True(textBuffer.Properties.TryGetProperty(typeof(BraceSmartIndenter), out BraceSmartIndenter smartIndenter));
        Assert.Same(expectedSmartIndenter, smartIndenter);
    }

    [Fact]
    public void TryInitializeTextBuffer_OnlyStoresSmartIndenterOnTextBufferOnce_ReturnsTrue()
    {
        // Arrange
        var factoryService = CreateFactoryService();
        var textBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == _razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict);
        factoryService.TryInitializeTextBuffer(textBuffer);
        var expectedSmartIndenter = textBuffer.Properties[typeof(BraceSmartIndenter)];

        // Create a second factory service so it generates a different smart indenter
        factoryService = CreateFactoryService();

        // Act
        var result = factoryService.TryInitializeTextBuffer(textBuffer);

        // Assert
        Assert.True(result);
        Assert.True(textBuffer.Properties.TryGetProperty(typeof(BraceSmartIndenter), out BraceSmartIndenter smartIndenter));
        Assert.Same(expectedSmartIndenter, smartIndenter);
    }

    private static DefaultRazorEditorFactoryService CreateFactoryService(
        VisualStudioDocumentTracker documentTracker = null,
        VisualStudioRazorParser parser = null,
        BraceSmartIndenter smartIndenter = null)
    {
        documentTracker ??= Mock.Of<VisualStudioDocumentTracker>(MockBehavior.Strict);
        parser ??= Mock.Of<VisualStudioRazorParser>(MockBehavior.Strict);
        smartIndenter ??= Mock.Of<BraceSmartIndenter>(MockBehavior.Strict);

        var documentTrackerFactory = Mock.Of<VisualStudioDocumentTrackerFactory>(f => f.Create(It.IsAny<ITextBuffer>()) == documentTracker, MockBehavior.Strict);
        var parserFactory = Mock.Of<VisualStudioRazorParserFactory>(f => f.Create(It.IsAny<VisualStudioDocumentTracker>()) == parser, MockBehavior.Strict);
        var smartIndenterFactory = Mock.Of<BraceSmartIndenterFactory>(f => f.Create(It.IsAny<VisualStudioDocumentTracker>()) == smartIndenter, MockBehavior.Strict);

        var services = TestServices.Create(new ILanguageService[]
        {
            documentTrackerFactory,
            parserFactory,
            smartIndenterFactory
        });

        Workspace workspace = TestWorkspace.Create(services);
        var workspaceAccessor = new Mock<VisualStudioWorkspaceAccessor>(MockBehavior.Strict);
        workspaceAccessor.Setup(p => p.TryGetWorkspace(It.IsAny<ITextBuffer>(), out workspace))
            .Returns(true);

        var factoryService = new DefaultRazorEditorFactoryService(workspaceAccessor.Object);

        return factoryService;
    }
}
