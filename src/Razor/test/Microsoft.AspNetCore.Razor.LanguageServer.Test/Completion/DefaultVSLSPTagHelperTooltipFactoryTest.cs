﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.Text.Adornments;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.AspNetCore.Razor.LanguageServer.Tooltip.DefaultVSLSPTagHelperTooltipFactory;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;

public class DefaultVSLSPTagHelperTooltipFactoryTest(ITestOutputHelper testOutput) : TestBase(testOutput)
{
    [Fact]
    public void CleanAndClassifySummaryContent_ClassifiedTextElement_ReplacesSeeCrefs()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = "Accepts <see cref=\"T:System.Collections.List{System.String}\" />s";

        // Act
        CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     Accepts List<string>s
        Assert.Collection(runs,
            run => AssertExpectedClassification(run, "Accepts ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void CleanSummaryContent_ClassifiedTextElement_ReplacesSeeAlsoCrefs()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = "Accepts <seealso cref=\"T:System.Collections.List{System.String}\" />s";

        // Act
        CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     Accepts List<string>s
        Assert.Collection(runs,
            run => AssertExpectedClassification(run, "Accepts ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
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
        CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     Hello
        //
        //     World
        Assert.Collection(runs, run => AssertExpectedClassification(
            run, "Hello" + Environment.NewLine + Environment.NewLine + "World", VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void CleanSummaryContent_ClassifiedTextElement_ClassifiesCodeBlocks()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = @"code: <code>This is code</code> and <code>This is some other code</code>.";

        // Act
        CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     code: This is code and This is some other code.
        Assert.Collection(runs,
            run => AssertExpectedClassification(run, "code: ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "This is code", VSPredefinedClassificationTypeNames.Text, ClassifiedTextRunStyle.UseClassificationFont),
            run => AssertExpectedClassification(run, " and ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "This is some other code", VSPredefinedClassificationTypeNames.Text, ClassifiedTextRunStyle.UseClassificationFont),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void CleanSummaryContent_ClassifiedTextElement_ClassifiesCBlocks()
    {
        // Arrange
        var runs = new List<ClassifiedTextRun>();
        var summary = @"code: <c>This is code</c> and <c>This is some other code</c>.";

        // Act
        CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     code: This is code and This is some other code.
        Assert.Collection(runs,
            run => AssertExpectedClassification(run, "code: ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "This is code", VSPredefinedClassificationTypeNames.Text, ClassifiedTextRunStyle.UseClassificationFont),
            run => AssertExpectedClassification(run, " and ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "This is some other code", VSPredefinedClassificationTypeNames.Text, ClassifiedTextRunStyle.UseClassificationFont),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Text));
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
        CleanAndClassifySummaryContent(runs, summary);

        // Assert

        // Expected output:
        //     code: This is code and This is some other code.
        Assert.Collection(runs, run => AssertExpectedClassification(
            run,
            "Summary description:" +
            Environment.NewLine +
            Environment.NewLine +
            "Paragraph text." +
            Environment.NewLine +
            Environment.NewLine +
            "End summary description.",
            VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_NoAssociatedTagHelperDescriptions_ReturnsFalse()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
        var elementDescription = AggregateBoundElementDescription.Empty;

        // Act
        var result = descriptionFactory.TryCreateTooltip(elementDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.False(result);
        Assert.Null(classifiedTextElement);
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_Element_SingleAssociatedTagHelper_ReturnsTrue_NestedTypes()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
        var associatedTagHelperInfos = new[]
        {
            new BoundElementDescriptionInfo(
                "Microsoft.AspNetCore.SomeTagHelper",
                "<summary>Uses <see cref=\"T:System.Collections.List{System.Collections.List{System.String}}\" />s</summary>"),
        };
        var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos.ToImmutableArray());

        // Act
        var result = descriptionFactory.TryCreateTooltip(elementDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.True(result);

        // Expected output:
        //     Microsoft.AspNetCore.SomeTagHelper
        //     Uses List<List<string>>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_Element_NamespaceContainsTypeName_ReturnsTrue()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
        var associatedTagHelperInfos = new[]
        {
            new BoundElementDescriptionInfo(
                "Microsoft.AspNetCore.SomeTagHelper.SomeTagHelper",
                "<summary>Uses <see cref=\"T:A.B.C{C.B}\" />s</summary>"),
        };
        var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos.ToImmutableArray());

        // Act
        var result = descriptionFactory.TryCreateTooltip(elementDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.True(result);

        // Expected output:
        //     Microsoft.AspNetCore.SomeTagHelper.SomeTagHelper
        //     Uses C<B>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "C", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "B", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_Element_MultipleAssociatedTagHelpers_ReturnsTrue()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
        var associatedTagHelperInfos = new[]
        {
            new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
            new BoundElementDescriptionInfo("Microsoft.AspNetCore.OtherTagHelper", "<summary>\nAlso uses <see cref=\"T:System.Collections.List{System.String}\" />s\n\r\n\r\r</summary>"),
        };
        var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos.ToImmutableArray());

        // Act
        var result = descriptionFactory.TryCreateTooltip(elementDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.True(result);

        // Expected output:
        //     Microsoft.AspNetCore.SomeTagHelper
        //     Uses List<string>s
        //
        //     Microsoft.AspNetCore.OtherTagHelper
        //     Also uses List<string>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "OtherTagHelper", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Also uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_NoAssociatedAttributeDescriptions_ReturnsFalse()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
        var elementDescription = AggregateBoundAttributeDescription.Empty;

        // Act
        var result = descriptionFactory.TryCreateTooltip(elementDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.False(result);
        Assert.Null(classifiedTextElement);
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_Attribute_SingleAssociatedAttribute_ReturnsTrue_NestedTypes()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
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
        var result = descriptionFactory.TryCreateTooltip(attributeDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.True(result);

        // Expected output:
        //     string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
        //     Uses List<List<string>>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, " ", VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTypeName", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeProperty", VSPredefinedClassificationTypeNames.Identifier),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ClassifiedTextElement_Attribute_MultipleAssociatedAttributes_ReturnsTrue()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
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
        var result = descriptionFactory.TryCreateTooltip(attributeDescription, out ClassifiedTextElement classifiedTextElement);

        // Assert
        Assert.True(result);

        // Expected output:
        //     string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
        //     Uses List<string>s
        //
        //     bool? Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName.AnotherProperty
        //     Uses List<string>s
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, " ", VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTypeName", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeProperty", VSPredefinedClassificationTypeNames.Identifier),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "bool", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, "?", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, " ", VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AnotherTypeName", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AnotherProperty", VSPredefinedClassificationTypeNames.Identifier),
            run => AssertExpectedClassification(run, Environment.NewLine, VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ContainerElement_NoAssociatedTagHelperDescriptions_ReturnsFalse()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
        var elementDescription = AggregateBoundElementDescription.Empty;

        // Act
        var result = descriptionFactory.TryCreateTooltip(elementDescription, out ContainerElement containerElement);

        // Assert
        Assert.False(result);
        Assert.Null(containerElement);
    }

    [Fact]
    public void TryCreateTooltip_ContainerElement_Attribute_MultipleAssociatedTagHelpers_ReturnsTrue()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
        var associatedTagHelperInfos = new[]
        {
            new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
            new BoundElementDescriptionInfo("Microsoft.AspNetCore.OtherTagHelper", "<summary>\nAlso uses <see cref=\"T:System.Collections.List{System.String}\" />s\n\r\n\r\r</summary>"),
        };
        var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos.ToImmutableArray());

        // Act
        var result = descriptionFactory.TryCreateTooltip(elementDescription, out ContainerElement container);

        // Assert
        Assert.True(result);
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
        Assert.Equal(ClassGlyph, innerContainer[0]);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelper", VSPredefinedClassificationTypeNames.Type));

        // Uses List<string>s
        innerContainer = ((ContainerElement)containerElements[1]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));

        // new line
        innerContainer = ((ContainerElement)containerElements[2]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Empty(classifiedTextElement.Runs);

        // [Class Glyph] Microsoft.AspNetCore.OtherTagHelper
        innerContainer = ((ContainerElement)containerElements[3]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[1];
        Assert.Equal(2, innerContainer.Count);
        Assert.Equal(ClassGlyph, innerContainer[0]);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "OtherTagHelper", VSPredefinedClassificationTypeNames.Type));

        // Also uses List<string>s
        innerContainer = ((ContainerElement)containerElements[4]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Also uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
    }

    [Fact]
    public void TryCreateTooltip_ContainerElement_NoAssociatedAttributeDescriptions_ReturnsFalse()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
        var elementDescription = AggregateBoundAttributeDescription.Empty;

        // Act
        var result = descriptionFactory.TryCreateTooltip(elementDescription, out ContainerElement containerElement);

        // Assert
        Assert.False(result);
        Assert.Null(containerElement);
    }

    [Fact]
    public void TryCreateTooltip_ContainerElement_Attribute_MultipleAssociatedAttributes_ReturnsTrue()
    {
        // Arrange
        var descriptionFactory = new DefaultVSLSPTagHelperTooltipFactory();
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
        var result = descriptionFactory.TryCreateTooltip(attributeDescription, out ContainerElement container);

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
        Assert.Equal(PropertyGlyph, innerContainer[0]);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, " ", VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTypeName", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeProperty", VSPredefinedClassificationTypeNames.Identifier));

        // Uses List<string>s
        innerContainer = ((ContainerElement)containerElements[1]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));

        // new line
        innerContainer = ((ContainerElement)containerElements[2]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Empty(classifiedTextElement.Runs);

        // [TagHelper Glyph] bool? Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName.AnotherProperty
        innerContainer = ((ContainerElement)containerElements[3]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[1];
        Assert.Equal(2, innerContainer.Count);
        Assert.Equal(PropertyGlyph, innerContainer[0]);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "bool", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, "?", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, " ", VSPredefinedClassificationTypeNames.WhiteSpace),
            run => AssertExpectedClassification(run, "Microsoft", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AspNetCore", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "SomeTagHelpers", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AnotherTypeName", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "AnotherProperty", VSPredefinedClassificationTypeNames.Identifier));

        // Uses List<string>s
        innerContainer = ((ContainerElement)containerElements[4]).Elements.ToList();
        classifiedTextElement = (ClassifiedTextElement)innerContainer[0];
        Assert.Single(innerContainer);
        Assert.Collection(classifiedTextElement.Runs,
            run => AssertExpectedClassification(run, "Uses ", VSPredefinedClassificationTypeNames.Text),
            run => AssertExpectedClassification(run, "List", VSPredefinedClassificationTypeNames.Type),
            run => AssertExpectedClassification(run, "<", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "string", VSPredefinedClassificationTypeNames.Keyword),
            run => AssertExpectedClassification(run, ">", VSPredefinedClassificationTypeNames.Punctuation),
            run => AssertExpectedClassification(run, "s", VSPredefinedClassificationTypeNames.Text));
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
