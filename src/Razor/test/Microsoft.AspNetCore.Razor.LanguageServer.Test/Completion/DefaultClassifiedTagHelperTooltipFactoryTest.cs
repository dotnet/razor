// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.Text.Adornments;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;

public class DefaultClassifiedTagHelperTooltipFactoryTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    [Fact]
    public void CleanAndClassifySummaryContent_ClassifiedTextElement_ReplacesSeeCrefs()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = "Accepts <see cref=\"T:System.Collections.List{System.String}\" />s";

        // Act
        DefaultClassifiedTagHelperTooltipFactory.CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     Accepts List<string>s
        Assert.Collection(runs,
            run => AssertExpectedClassification(run, "Accepts ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void CleanSummaryContent_ClassifiedTextElement_ReplacesSeeAlsoCrefs()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = "Accepts <seealso cref=\"T:System.Collections.List{System.String}\" />s";

        // Act
        DefaultClassifiedTagHelperTooltipFactory.CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     Accepts List<string>s
        Assert.Collection(runs,
            run => AssertExpectedClassification(run, "Accepts ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void CleanSummaryContent_ClassifiedTextElement_TrimsSurroundingWhitespace()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = @"
            Hello

    World

";

        // Act
        DefaultClassifiedTagHelperTooltipFactory.CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     Hello
        //
        //     World
        Assert.Collection(runs, run => AssertExpectedClassification(
            run, """
            Hello

            World
            """, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void CleanSummaryContent_ClassifiedTextElement_ClassifiesCodeBlocks()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = @"code: <code>This is code</code> and <code>This is some other code</code>.";

        // Act
        DefaultClassifiedTagHelperTooltipFactory.CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     code: This is code and This is some other code.
        Assert.Collection(runs,
            run => AssertExpectedClassification(run, "code: ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "This is code", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text, ClassifiedTextRunStyle.UseClassificationFont),
            run => AssertExpectedClassification(run, " and ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "This is some other code", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text, ClassifiedTextRunStyle.UseClassificationFont),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void CleanSummaryContent_ClassifiedTextElement_ClassifiesCBlocks()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = @"code: <c>This is code</c> and <c>This is some other code</c>.";

        // Act
        DefaultClassifiedTagHelperTooltipFactory.CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     code: This is code and This is some other code.
        Assert.Collection(runs,
            run => AssertExpectedClassification(run, "code: ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "This is code", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text, ClassifiedTextRunStyle.UseClassificationFont),
            run => AssertExpectedClassification(run, " and ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "This is some other code", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text, ClassifiedTextRunStyle.UseClassificationFont),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void CleanSummaryContent_ClassifiedTextElement_ParasCreateNewLines()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = @"Summary description:
<para>Paragraph text.</para>
End summary description.";

        // Act
        DefaultClassifiedTagHelperTooltipFactory.CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     code: This is code and This is some other code.
        Assert.Collection(runs, run => AssertExpectedClassification(
            run, """
            Summary description:

            Paragraph text.

            End summary description.
            """,
            DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public async Task TryCreateTooltip_ClassifiedTextElement_NoAssociatedTagHelperDescriptions_ReturnsFalse()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var elementDescription = AggregateBoundElementDescription.Empty;

        // Act
        var classifiedTextElement = await tooltipFactory.TryCreateTooltipAsync("file.razor", elementDescription, CancellationToken.None);

        // Assert
        Assert.Null(classifiedTextElement);
    }

    [Fact]
    public async Task TryCreateTooltip_ClassifiedTextElement_Element_SingleAssociatedTagHelper_ReturnsTrue_NestedTypes()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var associatedTagHelperInfos = new[]
        {
            new BoundElementDescriptionInfo(
                "Microsoft.AspNetCore.SomeTagHelper",
                "<summary>Uses <see cref=\"T:System.Collections.List{System.Collections.List{System.String}}\" />s</summary>"),
        };
        var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos.ToImmutableArray());

        // Act
        var classifiedTextElement = await tooltipFactory.TryCreateTooltipAsync("file.razor", elementDescription, CancellationToken.None);

        // Assert
        Assert.NotNull(classifiedTextElement);

        // Expected output:
        //     Microsoft.AspNetCore.SomeTagHelper
        //     Uses List<List<string>>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public async Task TryCreateTooltip_ClassifiedTextElement_Element_NamespaceContainsTypeName_ReturnsTrue()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var associatedTagHelperInfos = new[]
        {
            new BoundElementDescriptionInfo(
                "Microsoft.AspNetCore.SomeTagHelper.SomeTagHelper",
                "<summary>Uses <see cref=\"T:A.B.C{C.B}\" />s</summary>"),
        };
        var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos.ToImmutableArray());

        // Act
        var classifiedTextElement = await tooltipFactory.TryCreateTooltipAsync("file.razor", elementDescription, CancellationToken.None);

        // Assert
        Assert.NotNull(classifiedTextElement);

        // Expected output:
        //     Microsoft.AspNetCore.SomeTagHelper.SomeTagHelper
        //     Uses C<B>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "C", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "B", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public async Task TryCreateTooltip_ClassifiedTextElement_Element_MultipleAssociatedTagHelpers_ReturnsTrue()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var associatedTagHelperInfos = new[]
        {
            new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
            new BoundElementDescriptionInfo("Microsoft.AspNetCore.OtherTagHelper", "<summary>\nAlso uses <see cref=\"T:System.Collections.List{System.String}\" />s\n\r\n\r\r</summary>"),
        };
        var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos.ToImmutableArray());

        // Act
        var classifiedTextElement = await tooltipFactory.TryCreateTooltipAsync("file.razor", elementDescription, CancellationToken.None);

        // Assert
        Assert.NotNull(classifiedTextElement);

        // Expected output:
        //     Microsoft.AspNetCore.SomeTagHelper
        //     Uses List<string>s
        //
        //     Microsoft.AspNetCore.OtherTagHelper
        //     Also uses List<string>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "OtherTagHelper", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Also uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_NoAssociatedAttributeDescriptions_ReturnsFalse()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var elementDescription = AggregateBoundAttributeDescription.Empty;

        // Act
        var result = tooltipFactory.TryCreateTooltip(elementDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.False(result);
        Assert.Null(classifiedTextElement);
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_Attribute_SingleAssociatedAttribute_ReturnsTrue_NestedTypes()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var associatedAttributeDescriptions = new[]
        {
            new BoundAttributeDescriptionInfo(
                ReturnTypeName: "System.String",
                TypeName: "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName",
                PropertyName: "SomeProperty",
                Documentation: "<summary>Uses <see cref=\"T:System.Collections.List{System.Collections.List{System.String}}\" />s</summary>")
        };
        var attributeDescription = new AggregateBoundAttributeDescription(associatedAttributeDescriptions.ToImmutableArray());

        // Act
        var result = tooltipFactory.TryCreateTooltip(attributeDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.True(result);

        // Expected output:
        //     string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
        //     Uses List<List<string>>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, " ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTypeName", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeProperty", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Identifier),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_Attribute_MultipleAssociatedAttributes_ReturnsTrue()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var associatedAttributeDescriptions = new[]
        {
            new BoundAttributeDescriptionInfo(
                ReturnTypeName: "System.String",
                TypeName: "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName",
                PropertyName: "SomeProperty",
                Documentation: "<summary>Uses <see cref=\"T:System.Collections.List{System.String}\" />s</summary>"),
            new BoundAttributeDescriptionInfo(
                PropertyName: "AnotherProperty",
                TypeName: "Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName",
                ReturnTypeName: "System.Boolean?",
                Documentation: "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
        };
        var attributeDescription = new AggregateBoundAttributeDescription(associatedAttributeDescriptions.ToImmutableArray());

        // Act
        var result = tooltipFactory.TryCreateTooltip(attributeDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.True(result);

        // Expected output:
        //     string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
        //     Uses List<string>s
        //
        //     bool? Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName.AnotherProperty
        //     Uses List<string>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, " ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTypeName", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeProperty", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Identifier),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "bool", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, "?", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, " ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AnotherTypeName", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AnotherProperty", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Identifier),
            run => AssertExpectedClassification(run, Environment.NewLine, DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public async Task TryCreateTooltip_ContainerElement_NoAssociatedTagHelperDescriptions_ReturnsFalse()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var elementDescription = AggregateBoundElementDescription.Empty;

        // Act
        var containerElement = await tooltipFactory.TryCreateTooltipContainerAsync("file.razor", elementDescription, CancellationToken.None);

        // Assert
        Assert.Null(containerElement);
    }

    [Fact]
    public async Task TryCreateTooltip_ContainerElement_Attribute_MultipleAssociatedTagHelpers_ReturnsTrue()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var associatedTagHelperInfos = new[]
        {
            new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
            new BoundElementDescriptionInfo("Microsoft.AspNetCore.OtherTagHelper", "<summary>\nAlso uses <see cref=\"T:System.Collections.List{System.String}\" />s\n\r\n\r\r</summary>"),
        };
        var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos.ToImmutableArray());

        // Act
        var container = await tooltipFactory.TryCreateTooltipContainerAsync("file.razor", elementDescription, CancellationToken.None);

        // Assert
        Assert.NotNull(container);
        var containerElements = container.Elements.ToList();

        // Expected output:
        //     [Class Glyph] Microsoft.AspNetCore.SomeTagHelper
        //     Uses List<string>s
        //
        //     [Class Glyph] Microsoft.AspNetCore.OtherTagHelper
        //     Also uses List<string>s
        Assert.Equal(ContainerElementStyle.Stacked, container.Style);
        Assert.Equal(5, containerElements.Count);

        // [Class Glyph] Microsoft.AspNetCore.SomeTagHelper
        var innerContainer = ((ContainerElement)containerElements[0]).Elements.ToList();
        var classifiedTextElement = (ClassifiedTextElement)innerContainer[1];
        Assert.Equal(2, innerContainer.Count);
        Assert.Equal(DefaultClassifiedTagHelperTooltipFactory.ClassGlyph, innerContainer[0]);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type));

        // Uses List<string>s
        innerContainer = ((ContainerElement)containerElements[1]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));

        // new line
        innerContainer = ((ContainerElement)containerElements[2]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Empty(classifiedTextElement.Runs);

        // [Class Glyph] Microsoft.AspNetCore.OtherTagHelper
        innerContainer = ((ContainerElement)containerElements[3]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[1];
        Assert.Equal(2, innerContainer.Count);
        Assert.Equal(DefaultClassifiedTagHelperTooltipFactory.ClassGlyph, innerContainer[0]);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "OtherTagHelper", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type));

        // Also uses List<string>s
        innerContainer = ((ContainerElement)containerElements[4]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Also uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ContainerElement_NoAssociatedAttributeDescriptions_ReturnsFalse()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var elementDescription = AggregateBoundAttributeDescription.Empty;

        // Act
        var result = tooltipFactory.TryCreateTooltip(elementDescription, out ContainerElement containerElement);

        // Assert
        Assert.False(result);
        Assert.Null(containerElement);
    }

    [Fact]
    public void TryCreateTooltip_ContainerElement_Attribute_MultipleAssociatedAttributes_ReturnsTrue()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var tooltipFactory = new DefaultClassifiedTagHelperTooltipFactory(projectManager);
        var associatedAttributeDescriptions = new[]
        {
            new BoundAttributeDescriptionInfo(
                ReturnTypeName: "System.String",
                TypeName: "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName",
                PropertyName: "SomeProperty",
                Documentation: "<summary>Uses <see cref=\"T:System.Collections.List{System.String}\" />s</summary>"),
            new BoundAttributeDescriptionInfo(
                PropertyName: "AnotherProperty",
                TypeName: "Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName",
                ReturnTypeName: "System.Boolean?",
                Documentation: "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
        };
        var attributeDescription = new AggregateBoundAttributeDescription(associatedAttributeDescriptions.ToImmutableArray());

        // Act
        var result = tooltipFactory.TryCreateTooltip(attributeDescription, out ContainerElement container);

        // Assert
        Assert.True(result);
        var containerElements = container.Elements.ToList();

        // Expected output:
        //     [Property Glyph] string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
        //     Uses List<string>s
        //
        //     [Property Glyph] bool? Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName.AnotherProperty
        //     Uses List<string>s
        Assert.Equal(ContainerElementStyle.Stacked, container.Style);
        Assert.Equal(5, containerElements.Count);

        // [TagHelper Glyph] string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
        var innerContainer = ((ContainerElement)containerElements[0]).Elements.ToList();
        var classifiedTextElement = (ClassifiedTextElement)innerContainer[1];
        Assert.Equal(2, innerContainer.Count);
        Assert.Equal(DefaultClassifiedTagHelperTooltipFactory.PropertyGlyph, innerContainer[0]);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, " ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTypeName", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeProperty", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Identifier));

        // Uses List<string>s
        innerContainer = ((ContainerElement)containerElements[1]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));

        // new line
        innerContainer = ((ContainerElement)containerElements[2]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Empty(classifiedTextElement.Runs);

        // [TagHelper Glyph] bool? Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName.AnotherProperty
        innerContainer = ((ContainerElement)containerElements[3]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[1];
        Assert.Equal(2, innerContainer.Count);
        Assert.Equal(DefaultClassifiedTagHelperTooltipFactory.PropertyGlyph, innerContainer[0]);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "bool", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, "?", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, " ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AnotherTypeName", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AnotherProperty", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Identifier));

        // Uses List<string>s
        innerContainer = ((ContainerElement)containerElements[4]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Uses ", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", DefaultClassifiedTagHelperTooltipFactory.VSPredefinedClassificationTypeNames.Text));
    }

    internal static void AssertExpectedClassification(
        ClassifiedTextRun run,
        string expectedText,
        string expectedClassificationType,
        ClassifiedTextRunStyle expectedClassificationStyle = ClassifiedTextRunStyle.Plain)
    {
        Assert.Equal(expectedText, run.Text);
        Assert.Equal(expectedClassificationType, run.ClassificationTypeName);
        Assert.Equal(expectedClassificationStyle, run.Style);
    }
}
