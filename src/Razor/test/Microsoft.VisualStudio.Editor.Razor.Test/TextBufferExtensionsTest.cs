// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Editor.Razor;

public class TextBufferExtensionsTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [Fact]
    public void TryGetCodeDocument_SucceedsIfParserHasCodeDocument()
    {
        // Arrange
        var expectedCodeDocument = TestRazorCodeDocument.Create("Hello World");
        var parser = Mock.Of<VisualStudioRazorParser>(p =>
            p.CodeDocument == expectedCodeDocument,
            MockBehavior.Strict);
        var properties = new PropertyCollection()
        {
            [typeof(VisualStudioRazorParser)] = parser
        };

        var textBuffer = Mock.Of<ITextBuffer>(buffer =>
            buffer.Properties == properties,
            MockBehavior.Strict);

        // Act
        var result = textBuffer.TryGetCodeDocument(out var codeDocument);

        // Assert
        Assert.True(result);
        Assert.Same(expectedCodeDocument, codeDocument);
    }

    [Fact]
    public void TryGetCodeDocument_FailsIfParserMissingCodeDocument()
    {
        // Arrange
        var parser = Mock.Of<VisualStudioRazorParser>(p =>
            p.CodeDocument == null,
            MockBehavior.Strict);
        var properties = new PropertyCollection()
        {
            [typeof(VisualStudioRazorParser)] = parser
        };
        var textBuffer = Mock.Of<ITextBuffer>(buffer =>
            buffer.Properties == properties,
            MockBehavior.Strict);

        // Act
        var result = textBuffer.TryGetCodeDocument(out var codeDocument);

        // Assert
        Assert.False(result);
        Assert.Null(codeDocument);
    }

    [Fact]
    public void TryGetCodeDocument_FailsIfNoParserIsAvailable()
    {
        // Arrange
        var textBuffer = Mock.Of<ITextBuffer>(buffer =>
            buffer.Properties == new PropertyCollection(),
            MockBehavior.Strict);

        // Act
        var result = textBuffer.TryGetCodeDocument(out var codeDocument);

        // Assert
        Assert.False(result);
        Assert.Null(codeDocument);
    }
}
