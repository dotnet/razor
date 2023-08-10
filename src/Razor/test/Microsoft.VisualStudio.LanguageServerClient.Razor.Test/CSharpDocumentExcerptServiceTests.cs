﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

public class CSharpDocumentExcerptServiceTest : DocumentExcerptServiceTestBase
{
    public CSharpDocumentExcerptServiceTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public async Task TryGetExcerptInternalAsync_SingleLine_CanClassifyCSharp()
    {
        // Arrange
        var razorSource = @"
<html>
@{
    var [|foo|] = ""Hello, World!"";
}
  <body>@foo</body>
  <div>@(3 + 4)</div><div>@(foo + foo)</div>
</html>
";

        var (generatedDocument, razorSourceText, primarySpan, generatedSpan) = await InitializeAsync(razorSource);

#pragma warning disable CS0618 // Type or member is obsolete
        var excerptService = new CSharpDocumentExcerptService();
#pragma warning restore CS0618 // Type or member is obsolete
        var mappedLinePositionSpan = razorSourceText.Lines.GetLinePositionSpan(primarySpan);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await excerptService.TryGetExcerptInternalAsync(
            generatedDocument,
            generatedSpan,
            ExcerptModeInternal.SingleLine,
            razorSourceText,
            mappedLinePositionSpan,
            options,
            DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(generatedSpan, result.Value.Span);
        Assert.Same(generatedDocument, result.Value.Document);

        // Verifies that the right part of the primary document will be highlighted.
        Assert.Equal(
            (await generatedDocument.GetTextAsync()).GetSubText(generatedSpan).ToString(),
            result.Value.Content.GetSubText(result.Value.MappedSpan).ToString(),
            ignoreLineEndingDifferences: true);

        Assert.Equal(@"var foo = ""Hello, World!"";", result.Value.Content.ToString(), ignoreLineEndingDifferences: true);
        Assert.Collection(
            result.Value.ClassifiedSpans,
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Keyword, c.ClassificationType);
                Assert.Equal("var", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(" ", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.LocalName, c.ClassificationType);
                Assert.Equal("foo", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(" ", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Operator, c.ClassificationType);
                Assert.Equal("=", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(" ", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.StringLiteral, c.ClassificationType);
                Assert.Equal("\"Hello, World!\"", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Punctuation, c.ClassificationType);
                Assert.Equal(";", result.Value.Content.GetSubText(c.TextSpan).ToString());
            });
    }

    [Fact]
    public async Task TryGetExcerptInternalAsync_SingleLine_CanClassifyCSharp_ImplicitExpression()
    {
        // Arrange
        var razorSource = @"
<html>
@{
    var foo = ""Hello, World!"";
}
  <body>@[|foo|]</body>
  <div>@(3 + 4)</div><div>@(foo + foo)</div>
</html>
";

        var (generatedDocument, razorSourceText, primarySpan, generatedSpan) = await InitializeAsync(razorSource);

#pragma warning disable CS0618 // Type or member is obsolete
        var excerptService = new CSharpDocumentExcerptService();
#pragma warning restore CS0618 // Type or member is obsolete
        var mappedLinePositionSpan = razorSourceText.Lines.GetLinePositionSpan(primarySpan);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await excerptService.TryGetExcerptInternalAsync(
            generatedDocument,
            generatedSpan,
            ExcerptModeInternal.SingleLine,
            razorSourceText,
            mappedLinePositionSpan,
            options,
            DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(generatedSpan, result.Value.Span);
        Assert.Same(generatedDocument, result.Value.Document);

        // Verifies that the right part of the primary document will be highlighted.
        Assert.Equal(
            (await generatedDocument.GetTextAsync()).GetSubText(generatedSpan).ToString(),
            result.Value.Content.GetSubText(result.Value.MappedSpan).ToString(),
            ignoreLineEndingDifferences: true);

        Assert.Equal(@"<body>@foo</body>", result.Value.Content.ToString(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public async Task TryGetExcerptInternalAsync_SingleLine_CanClassifyCSharp_ComplexLine()
    {
        // Arrange
        var razorSource = @"
<html>
@{
    var foo = ""Hello, World!"";
}
  <body>@foo</body>
  <div>@(3 + 4)</div><div>@(foo + [|foo|])</div>
</html>
";

        var (generatedDocument, razorSourceText, primarySpan, generatedSpan) = await InitializeAsync(razorSource);

#pragma warning disable CS0618 // Type or member is obsolete
        var excerptService = new CSharpDocumentExcerptService();
#pragma warning restore CS0618 // Type or member is obsolete
        var mappedLinePositionSpan = razorSourceText.Lines.GetLinePositionSpan(primarySpan);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await excerptService.TryGetExcerptInternalAsync(
            generatedDocument,
            generatedSpan,
            ExcerptModeInternal.SingleLine,
            razorSourceText,
            mappedLinePositionSpan,
            options,
            DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(generatedSpan, result.Value.Span);
        Assert.Same(generatedDocument, result.Value.Document);

        // Verifies that the right part of the primary document will be highlighted.
        Assert.Equal(
            (await generatedDocument.GetTextAsync()).GetSubText(generatedSpan).ToString(),
            result.Value.Content.GetSubText(result.Value.MappedSpan).ToString(),
            ignoreLineEndingDifferences: true);

        // Verifies that the right part of the primary document will be highlighted.
        Assert.Equal(@"<div>@(3 + 4)</div><div>@(foo + foo)</div>", result.Value.Content.ToString(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public async Task TryGetExcerptInternalAsync_MultiLine_CanClassifyCSharp()
    {
        // Arrange
        var razorSource = @"
<html>
@{
    var [|foo|] = ""Hello, World!"";
}
  <body></body>
  <div></div>
</html>
";

        var (generatedDocument, razorSourceText, primarySpan, generatedSpan) = await InitializeAsync(razorSource);

#pragma warning disable CS0618 // Type or member is obsolete
        var excerptService = new CSharpDocumentExcerptService();
#pragma warning restore CS0618 // Type or member is obsolete
        var mappedLinePositionSpan = razorSourceText.Lines.GetLinePositionSpan(primarySpan);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await excerptService.TryGetExcerptInternalAsync(
            generatedDocument,
            generatedSpan,
            ExcerptModeInternal.Tooltip,
            razorSourceText,
            mappedLinePositionSpan,
            options,
            DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(generatedSpan, result.Value.Span);
        Assert.Same(generatedDocument, result.Value.Document);

        // Verifies that the right part of the primary document will be highlighted.
        Assert.Equal(
            (await generatedDocument.GetTextAsync()).GetSubText(generatedSpan).ToString(),
            result.Value.Content.GetSubText(result.Value.MappedSpan).ToString(),
            ignoreLineEndingDifferences: true);

        Assert.Equal(
@"
<html>
@{
    var foo = ""Hello, World!"";
}
  <body></body>
  <div></div>",
            result.Value.Content.ToString(), ignoreLineEndingDifferences: true);
    }

    [Fact]
    public async Task TryGetExcerptInternalAsync_MultiLine_Boundaries_CanClassifyCSharp()
    {
        // Arrange
        var razorSource = @"@{ var [|foo|] = ""Hello, World!""; }";

        var (generatedDocument, razorSourceText, primarySpan, generatedSpan) = await InitializeAsync(razorSource);

#pragma warning disable CS0618 // Type or member is obsolete
        var excerptService = new CSharpDocumentExcerptService();
#pragma warning restore CS0618 // Type or member is obsolete
        var mappedLinePositionSpan = razorSourceText.Lines.GetLinePositionSpan(primarySpan);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await excerptService.TryGetExcerptInternalAsync(
            generatedDocument,
            generatedSpan,
            ExcerptModeInternal.Tooltip,
            razorSourceText,
            mappedLinePositionSpan,
            options,
            DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(generatedSpan, result.Value.Span);
        Assert.Same(generatedDocument, result.Value.Document);

        // Verifies that the right part of the primary document will be highlighted.
        Assert.Equal(
            (await generatedDocument.GetTextAsync()).GetSubText(generatedSpan).ToString(),
            result.Value.Content.GetSubText(result.Value.MappedSpan).ToString(),
            ignoreLineEndingDifferences: true);

        Assert.Equal(
@"@{ var foo = ""Hello, World!""; }",
            result.Value.Content.ToString(), ignoreLineEndingDifferences: true);
    }
}
