// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

public class FormattingUtilitiesTest
{
    [Theory]
    [InlineData(1, 4, 0)]
    [InlineData(2, 4, 0)]
    [InlineData(3, 4, 0)]
    [InlineData(1, 4, 1)]
    [InlineData(2, 4, 2)]
    [InlineData(3, 4, 3)]
    [InlineData(4, 8, 6)]
    public void GetIndentationLevel_Spaces(int level, int tabSize, int additional)
    {
        var input = new string(' ', level * tabSize + additional);
        var text = SourceText.From(input);

        var actual = FormattingUtilities.GetIndentationLevel(text.Lines[0], text.Length, insertSpaces: true, tabSize, out var additionalIndentation);

        Assert.Equal(level, actual);
        Assert.Equal(additional, additionalIndentation.Length);
        Assert.All(additionalIndentation, c => Assert.Equal(' ', c));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 6)]
    public void GetIndentationLevel_Tabs(int level, int additional)
    {
        var input = new string('\t', level) + new string(' ', additional);
        var text = SourceText.From(input);

        var actual = FormattingUtilities.GetIndentationLevel(text.Lines[0], text.Length, insertSpaces: false, tabSize: 4, out var additionalIndentation);

        Assert.Equal(level, actual);
        Assert.Equal(additional, additionalIndentation.Length);
        Assert.All(additionalIndentation, c => Assert.Equal(' ', c));
    }
}
