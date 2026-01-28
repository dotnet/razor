// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.CohostingShared;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Razor.LanguageClient.Cohost;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.RazorExtension.Test.Endpoints.Shared;

public class RazorSourceGeneratedDocumentExcerptServiceTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task TryExcerptAsync_SingleLine_CanClassifyCSharp()
    {
        // Arrange
        TestCode razorSource = """
            <html>
            @{
                var [|foo|] = "Hello, World!";
            }
              <body>@foo</body>
              <div>@(3 + 4)</div><div>@(foo + foo)</div>
            </html>
            """;

        var (generatedDocument, generatedSpan) = await GetGeneratedDocumentAndSpanAsync(razorSource, DisposalToken);

        var service = new RazorSourceGeneratedDocumentExcerptService(RemoteServiceInvoker);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await service.TryExcerptAsync(generatedDocument, generatedSpan, RazorExcerptMode.SingleLine, options, DisposalToken);

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
                Assert.Equal("""
                    "Hello, World!"
                    """, result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Punctuation, c.ClassificationType);
                Assert.Equal(";", result.Value.Content.GetSubText(c.TextSpan).ToString());
            });
    }

    [Fact]
    public async Task TryExcerptAsync_SingleLine_CanClassifyCSharp_ImplicitExpression()
    {
        // Arrange
        var razorSource = """
            <html>
            @{
                var foo = "Hello, World!";
            }
              <body>@[|foo|]</body>
              <div>@(3 + 4)</div><div>@(foo + foo)</div>
            </html>
            """;

        var (generatedDocument, generatedSpan) = await GetGeneratedDocumentAndSpanAsync(razorSource, DisposalToken);

        var service = new RazorSourceGeneratedDocumentExcerptService(RemoteServiceInvoker);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await service.TryExcerptAsync(generatedDocument, generatedSpan, RazorExcerptMode.SingleLine, options, DisposalToken);

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
        Assert.Collection(
            result.Value.ClassifiedSpans,
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal("<body>@", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.LocalName, c.ClassificationType);
                Assert.Equal("foo", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal("</body>", result.Value.Content.GetSubText(c.TextSpan).ToString());
            });
    }

    [Fact]
    public async Task TryExcerptAsync_SingleLine_CanClassifyCSharp_ComplexLine()
    {
        // Arrange
        var razorSource = """
            <html>
            @{
                var foo = "Hello, World!";
            }
              <body>@foo</body>
              <div>@(3 + 4)</div><div>@(foo + [|foo|])</div>
            </html>
            """;

        var (generatedDocument, generatedSpan) = await GetGeneratedDocumentAndSpanAsync(razorSource, DisposalToken);

        var service = new RazorSourceGeneratedDocumentExcerptService(RemoteServiceInvoker);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await service.TryExcerptAsync(generatedDocument, generatedSpan, RazorExcerptMode.SingleLine, options, DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(generatedSpan, result.Value.Span);
        Assert.Same(generatedDocument, result.Value.Document);

        Assert.Equal(
            (await generatedDocument.GetTextAsync()).GetSubText(generatedSpan).ToString(),
            result.Value.Content.GetSubText(result.Value.MappedSpan).ToString(),
            ignoreLineEndingDifferences: true);

        // Verifies that the right part of the primary document will be highlighted.
        Assert.Equal(@"<div>@(3 + 4)</div><div>@(foo + foo)</div>", result.Value.Content.ToString(), ignoreLineEndingDifferences: true);
        Assert.Collection(
            result.Value.ClassifiedSpans,
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal("<div>@(", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.NumericLiteral, c.ClassificationType);
                Assert.Equal("3", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(" ", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Operator, c.ClassificationType);
                Assert.Equal("+", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(" ", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.NumericLiteral, c.ClassificationType);
                Assert.Equal("4", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(")</div><div>@(", result.Value.Content.GetSubText(c.TextSpan).ToString());
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
                Assert.Equal("+", result.Value.Content.GetSubText(c.TextSpan).ToString());
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
                Assert.Equal(")</div>", result.Value.Content.GetSubText(c.TextSpan).ToString());
            });
    }

    [Fact]
    public async Task TryExcerptAsync_MultiLine_MultilineString()
    {
        // Arrange
        var razorSource = """
                <html>
                @{
                    [|string|] bigString = @"
                        Razor shows 3 lines in a
                        tooltip maximum, so this
                        multi-line verbatim
                        string must be longer
                        than that.
                        ";
                }
                </html>
                """;

        var (generatedDocument, generatedSpan) = await GetGeneratedDocumentAndSpanAsync(razorSource, DisposalToken);

        var service = new RazorSourceGeneratedDocumentExcerptService(RemoteServiceInvoker);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await service.TryExcerptAsync(generatedDocument, generatedSpan, RazorExcerptMode.Tooltip, options, DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(generatedSpan, result.Value.Span);
        Assert.Same(generatedDocument, result.Value.Document);

        // Verifies that the right part of the primary document will be highlighted.
        Assert.Equal(
            (await generatedDocument.GetTextAsync()).GetSubText(generatedSpan).ToString(),
            result.Value.Content.GetSubText(result.Value.MappedSpan).ToString(),
            ignoreLineEndingDifferences: true);

        Assert.Equal("""
                <html>
                @{
                    string bigString = @"
                        Razor shows 3 lines in a
                        tooltip maximum, so this
                        multi-line verbatim
                """,
            result.Value.Content.ToString(), ignoreLineEndingDifferences: true);

        Assert.Collection(
            result.Value.ClassifiedSpans,
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal("""
                        <html>
                        @{
                        """,
                    result.Value.Content.GetSubText(c.TextSpan).ToString(),
                    ignoreLineEndingDifferences: true);
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal($"{Environment.NewLine}    ", result.Value.Content.GetSubText(c.TextSpan).ToString(), ignoreLineEndingDifferences: true);
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Keyword, c.ClassificationType);
                Assert.Equal("string", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(" ", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.LocalName, c.ClassificationType);
                Assert.Equal("bigString", result.Value.Content.GetSubText(c.TextSpan).ToString());
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
                Assert.Equal(ClassificationTypeNames.VerbatimStringLiteral, c.ClassificationType);
                Assert.Equal("""
                        @"
                                Razor shows 3 lines in a
                                tooltip maximum, so this
                                multi-line verbatim
                        """, result.Value.Content.GetSubText(c.TextSpan).ToString());
            });
    }

    [Fact]
    public async Task TryExcerptAsync_SingleLine_MultilineString()
    {
        // Arrange
        var razorSource = """
                <html>
                @{
                    [|string|] bigString = @"
                        This is a
                        multi-line verbatim
                        string.
                        ";
                }
                </html>
                """;

        var (generatedDocument, generatedSpan) = await GetGeneratedDocumentAndSpanAsync(razorSource, DisposalToken);

        var service = new RazorSourceGeneratedDocumentExcerptService(RemoteServiceInvoker);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await service.TryExcerptAsync(generatedDocument, generatedSpan, RazorExcerptMode.SingleLine, options, DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(generatedSpan, result.Value.Span);
        Assert.Same(generatedDocument, result.Value.Document);

        // Verifies that the right part of the primary document will be highlighted.
        Assert.Equal(
            (await generatedDocument.GetTextAsync()).GetSubText(generatedSpan).ToString(),
            result.Value.Content.GetSubText(result.Value.MappedSpan).ToString(),
            ignoreLineEndingDifferences: true);

        Assert.Equal("""
            string bigString = @"
            """, result.Value.Content.ToString(), ignoreLineEndingDifferences: true);

        Assert.Collection(
            result.Value.ClassifiedSpans,
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Keyword, c.ClassificationType);
                Assert.Equal("string", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(" ", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.LocalName, c.ClassificationType);
                Assert.Equal("bigString", result.Value.Content.GetSubText(c.TextSpan).ToString());
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
                Assert.Equal(ClassificationTypeNames.VerbatimStringLiteral, c.ClassificationType);
                Assert.Equal("""
                    @"
                    """, result.Value.Content.GetSubText(c.TextSpan).ToString());
            });
    }

    [Fact]
    public async Task TryGetExcerptInternalAsync_MultiLine_CanClassifyCSharp()
    {
        // Arrange
        var razorSource = """
            <html>
            @{
                var [|foo|] = "Hello, World!";
            }
              <body></body>
              <div></div>
            </html>
            """;

        var (generatedDocument, generatedSpan) = await GetGeneratedDocumentAndSpanAsync(razorSource, DisposalToken);

        var service = new RazorSourceGeneratedDocumentExcerptService(RemoteServiceInvoker);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await service.TryExcerptAsync(generatedDocument, generatedSpan, RazorExcerptMode.Tooltip, options, DisposalToken);

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
            """
            <html>
            @{
                var foo = "Hello, World!";
            }
              <body></body>
              <div></div>
            """,
            result.Value.Content.ToString(), ignoreLineEndingDifferences: true);

        Assert.Collection(
            result.Value.ClassifiedSpans,
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(
                    """
                    <html>
                    @{
                    """,
                        result.Value.Content.GetSubText(c.TextSpan).ToString(),
                        ignoreLineEndingDifferences: true);
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal($"{Environment.NewLine}    ", result.Value.Content.GetSubText(c.TextSpan).ToString(), ignoreLineEndingDifferences: true);
            },
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
                Assert.Equal("""
                    "Hello, World!"
                    """, result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Punctuation, c.ClassificationType);
                Assert.Equal(";", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(Environment.NewLine, result.Value.Content.GetSubText(c.TextSpan).ToString(), ignoreLineEndingDifferences: true);
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(
                    """
                    }
                      <body></body>
                      <div></div>
                    """,
                    result.Value.Content.GetSubText(c.TextSpan).ToString(),
                    ignoreLineEndingDifferences: true);
            });
    }

    [Fact]
    public async Task TryExcerptAsync_MultiLine_Boundaries_CanClassifyCSharp()
    {
        // Arrange
        var razorSource = @"@{ var [|foo|] = ""Hello, World!""; }";

        var (generatedDocument, generatedSpan) = await GetGeneratedDocumentAndSpanAsync(razorSource, DisposalToken);

        var service = new RazorSourceGeneratedDocumentExcerptService(RemoteServiceInvoker);

        // Act
        var options = RazorClassificationOptionsWrapper.Default;
        var result = await service.TryExcerptAsync(generatedDocument, generatedSpan, RazorExcerptMode.Tooltip, options, DisposalToken);

        // Assert
        // Verifies that the right part of the primary document will be highlighted.
        Assert.NotNull(result);
        Assert.Equal(generatedSpan, result.Value.Span);
        Assert.Same(generatedDocument, result.Value.Document);

        Assert.Equal(
            (await generatedDocument.GetTextAsync()).GetSubText(generatedSpan).ToString(),
            result.Value.Content.GetSubText(result.Value.MappedSpan).ToString(),
            ignoreLineEndingDifferences: true);

        Assert.Equal(@"@{ var foo = ""Hello, World!""; }", result.Value.Content.ToString(), ignoreLineEndingDifferences: true);

        Assert.Collection(
            result.Value.ClassifiedSpans,
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal("@{", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(" ", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
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
                Assert.Equal("""
                    "Hello, World!"
                    """, result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Punctuation, c.ClassificationType);
                Assert.Equal(";", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal(" ", result.Value.Content.GetSubText(c.TextSpan).ToString());
            },
            c =>
            {
                Assert.Equal(ClassificationTypeNames.Text, c.ClassificationType);
                Assert.Equal("}", result.Value.Content.GetSubText(c.TextSpan).ToString());
            });
    }

    private async Task<(SourceGeneratedDocument, TextSpan)> GetGeneratedDocumentAndSpanAsync(TestCode razorSource, CancellationToken cancellationToken)
    {
        var document = CreateProjectAndRazorDocument(razorSource.Text);

        var sourceText = await document.GetTextAsync(cancellationToken);

        var documentMappingService = OOPExportProvider.GetExportedValue<IDocumentMappingService>();
        var snapshotManager = OOPExportProvider.GetExportedValue<RemoteSnapshotManager>();

        var generatedDocument = await snapshotManager.GetSnapshot(document).GetGeneratedDocumentAsync(cancellationToken);
        var generatedSourceText = await generatedDocument.GetTextAsync(cancellationToken);
        var codeDocument = await snapshotManager.GetSnapshot(document).GetGeneratedOutputAsync(cancellationToken);
        var csharpDocument = codeDocument.GetRequiredCSharpDocument();

        var razorRange = sourceText.GetLinePositionSpan(razorSource.Span);
        Assert.True(documentMappingService.TryMapToCSharpDocumentRange(csharpDocument, razorRange, out var csharpRange));
        var csharpSpan = generatedSourceText.GetTextSpan(csharpRange);

        return (generatedDocument, csharpSpan);
    }
}
