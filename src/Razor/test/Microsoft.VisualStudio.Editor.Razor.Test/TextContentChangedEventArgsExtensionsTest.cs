﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.Test;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Text;

public class TextContentChangedEventArgsExtensionsTest : TestBase
{
    public TextContentChangedEventArgsExtensionsTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public void TextChangeOccurred_NoChanges_ReturnsFalse()
    {
        // Arrange
        var before = new StringTextSnapshot(string.Empty);
        var after = new StringTextSnapshot(string.Empty);
        var testArgs = new TestTextContentChangedEventArgs(before, after);

        // Act
        var result = testArgs.TextChangeOccurred(out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TextChangeOccurred_CancelingChanges_ReturnsFalse()
    {
        // Arrange
        var before = new StringTextSnapshot("by");
        before.Version.Changes.Add(new TestTextChange(new SourceChange(0, 2, "hi")));
        before.Version.Changes.Add(new TestTextChange(new SourceChange(0, 2, "by")));
        var after = new StringTextSnapshot("by");
        var testArgs = new TestTextContentChangedEventArgs(before, after);

        // Act
        var result = testArgs.TextChangeOccurred(out _);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void TextChangeOccurred_SingleChange_ReturnsTrue()
    {
        // Arrange
        var before = new StringTextSnapshot("by");
        var firstChange = new TestTextChange(new SourceChange(0, 2, "hi"));
        before.Version.Changes.Add(firstChange);
        var after = new StringTextSnapshot("hi");
        var testArgs = new TestTextContentChangedEventArgs(before, after);

        // Act
        var result = testArgs.TextChangeOccurred(out var changeInformation);

        // Assert
        Assert.True(result);
        Assert.Same(firstChange, changeInformation.firstChange);
        Assert.Equal(firstChange, changeInformation.lastChange);
        Assert.Equal("hi", changeInformation.newText);
        Assert.Equal("by", changeInformation.oldText);
    }

    [Fact]
    public void TextChangeOccurred_MultipleChanges_ReturnsTrue()
    {
        // Arrange
        var before = new StringTextSnapshot("by by");
        var firstChange = new TestTextChange(new SourceChange(0, 2, "hi"));
        before.Version.Changes.Add(firstChange);
        var lastChange = new TestTextChange(new SourceChange(3, 2, "hi"));
        before.Version.Changes.Add(lastChange);
        var after = new StringTextSnapshot("hi hi");
        var testArgs = new TestTextContentChangedEventArgs(before, after);

        // Act
        var result = testArgs.TextChangeOccurred(out var changeInformation);

        // Assert
        Assert.True(result);
        Assert.Same(firstChange, changeInformation.firstChange);
        Assert.Equal(lastChange, changeInformation.lastChange);
        Assert.Equal("hi hi", changeInformation.newText);
        Assert.Equal("by by", changeInformation.oldText);
    }

    private class TestTextContentChangedEventArgs : TextContentChangedEventArgs
    {
        public TestTextContentChangedEventArgs(ITextSnapshot before, ITextSnapshot after)
            : base(before, after, EditOptions.DefaultMinimalChange, null)
        {
        }
    }
}
