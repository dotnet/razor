// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Xunit;
using static Microsoft.AspNetCore.Razor.LanguageServer.Tooltip.DefaultTagHelperTooltipFactory;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    public class DefaultTagHelperDescriptionFactoryTest_ClassifiedTextElement : DefaultTagHelperDescriptionFactoryTestBase
    {
        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_NoAssociatedTagHelperDescriptions_ReturnsFalse()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(GetLanguageServer(supportsVisualStudioExtensions: true));
            var elementDescription = AggregateBoundElementDescription.Default;

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out RazorClassifiedTextElement markdown);

            // Assert
            Assert.False(result);
            Assert.Null(markdown);
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Element_SingleAssociatedTagHelper_ReturnsTrue_NestedTypes()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(GetLanguageServer(supportsVisualStudioExtensions: true));
            var associatedTagHelperInfos = new[]
            {
                new BoundElementDescriptionInfo(
                    "Microsoft.AspNetCore.SomeTagHelper",
                    "<summary>Uses <see cref=\"T:System.Collections.List{System.Collections.List{System.String}}\" />s</summary>"),
            };
            var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos);

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out RazorClassifiedTextElement classifiedTextElement);

            // Assert
            Assert.True(result);

            var runs = classifiedTextElement.Runs.ToArray();
            Assert.Equal(15, runs.Length);

            // Expected output:
            //     Microsoft.AspNetCore.SomeTagHelper
            //     Uses List<List<string>>s
            AssertExpectedClassification(runs[0], "Microsoft", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[1], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[2], "AspNetCore", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[3], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[4], "SomeTagHelper", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[5], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[6], "Uses ", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[7], "List", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[8], "<", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[9], "List", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[10], "<", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[11], "string", RazorPredefinedClassificationTypeNames.Keyword);
            AssertExpectedClassification(runs[12], ">", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[13], ">", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[14], "s", RazorPredefinedClassificationTypeNames.Text);
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Element_MultipleAssociatedTagHelpers_ReturnsTrue()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(GetLanguageServer(supportsVisualStudioExtensions: true));
            var associatedTagHelperInfos = new[]
            {
                new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
                new BoundElementDescriptionInfo("Microsoft.AspNetCore.OtherTagHelper", "<summary>\nAlso uses <see cref=\"T:System.Collections.List{System.String}\" />s\n\r\n\r\r</summary>"),
            };
            var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos);

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out RazorClassifiedTextElement classifiedTextElement);

            // Assert
            Assert.True(result);

            var runs = classifiedTextElement.Runs.ToArray();
            Assert.Equal(26, runs.Length);

            // Expected output:
            //     Microsoft.AspNetCore.SomeTagHelper
            //     Uses List<string>s
            //
            //     Microsoft.AspNetCore.OtherTagHelper
            //     Also uses List<string>s
            AssertExpectedClassification(runs[0], "Microsoft", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[1], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[2], "AspNetCore", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[3], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[4], "SomeTagHelper", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[5], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[6], "Uses ", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[7], "List", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[8], "<", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[9], "string", RazorPredefinedClassificationTypeNames.Keyword);
            AssertExpectedClassification(runs[10], ">", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[11], "s", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[12], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[13], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[14], "Microsoft", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[15], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[16], "AspNetCore", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[17], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[18], "OtherTagHelper", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[19], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[20], "Also uses ", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[21], "List", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[22], "<", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[23], "string", RazorPredefinedClassificationTypeNames.Keyword);
            AssertExpectedClassification(runs[24], ">", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[25], "s", RazorPredefinedClassificationTypeNames.Text);
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_NoAssociatedAttributeDescriptions_ReturnsFalse()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(GetLanguageServer(supportsVisualStudioExtensions: true));
            var elementDescription = AggregateBoundAttributeDescription.Default;

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out RazorClassifiedTextElement markdown);

            // Assert
            Assert.False(result);
            Assert.Null(markdown);
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Attribute_SingleAssociatedAttribute_ReturnsTrue_NestedTypes()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(GetLanguageServer(supportsVisualStudioExtensions: true));
            var associatedAttributeDescriptions = new[]
            {
                new BoundAttributeDescriptionInfo(
                    returnTypeName: "System.String",
                    typeName: "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName",
                    propertyName: "SomeProperty",
                    documentation: "<summary>Uses <see cref=\"T:System.Collections.List{System.Collections.List{System.String}}\" />s</summary>")
            };
            var attributeDescription = new AggregateBoundAttributeDescription(associatedAttributeDescriptions);

            // Act
            var result = descriptionFactory.TryCreateTooltip(attributeDescription, out RazorClassifiedTextElement classifiedTextElement);

            // Assert
            Assert.True(result);

            var runs = classifiedTextElement.Runs.ToArray();
            Assert.Equal(21, runs.Length);

            // Expected output:
            //     string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
            //     Uses List<List<string>>s
            AssertExpectedClassification(runs[0], "string", RazorPredefinedClassificationTypeNames.Keyword);
            AssertExpectedClassification(runs[1], " ", RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[2], "Microsoft", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[3], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[4], "AspNetCore", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[5], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[6], "SomeTagHelpers", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[7], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[8], "SomeTypeName", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[9], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[10], "SomeProperty", RazorPredefinedClassificationTypeNames.Identifier);
            AssertExpectedClassification(runs[11], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[12], "Uses ", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[13], "List", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[14], "<", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[15], "List", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[16], "<", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[17], "string", RazorPredefinedClassificationTypeNames.Keyword);
            AssertExpectedClassification(runs[18], ">", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[19], ">", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[20], "s", RazorPredefinedClassificationTypeNames.Text);
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Attribute_MultipleAssociatedAttributes_ReturnsTrue()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(GetLanguageServer(supportsVisualStudioExtensions: true));
            var associatedAttributeDescriptions = new[]
            {
                new BoundAttributeDescriptionInfo(
                    returnTypeName: "System.String",
                    typeName: "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName",
                    propertyName: "SomeProperty",
                    documentation: "<summary>Uses <see cref=\"T:System.Collections.List{System.String}\" />s</summary>"),
                new BoundAttributeDescriptionInfo(
                    propertyName: "AnotherProperty",
                    typeName: "Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName",
                    returnTypeName: "System.Boolean?",
                    documentation: "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
            };
            var attributeDescription = new AggregateBoundAttributeDescription(associatedAttributeDescriptions);

            // Act
            var result = descriptionFactory.TryCreateTooltip(attributeDescription, out RazorClassifiedTextElement classifiedTextElement);

            // Assert
            Assert.True(result);

            var runs = classifiedTextElement.Runs.ToArray();
            Assert.Equal(39, runs.Length);

            // Expected output:
            //     string Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeProperty
            //     Uses List<string>s
            //
            //     bool? Microsoft.AspNetCore.SomeTagHelpers.AnotherTypeName.AnotherProperty
            //     Uses List<string>s
            AssertExpectedClassification(runs[0], "string", RazorPredefinedClassificationTypeNames.Keyword);
            AssertExpectedClassification(runs[1], " ", RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[2], "Microsoft", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[3], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[4], "AspNetCore", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[5], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[6], "SomeTagHelpers", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[7], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[8], "SomeTypeName", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[9], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[10], "SomeProperty", RazorPredefinedClassificationTypeNames.Identifier);
            AssertExpectedClassification(runs[11], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[12], "Uses ", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[13], "List", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[14], "<", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[15], "string", RazorPredefinedClassificationTypeNames.Keyword);
            AssertExpectedClassification(runs[16], ">", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[17], "s", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[18], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[19], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[20], "bool", RazorPredefinedClassificationTypeNames.Keyword);
            AssertExpectedClassification(runs[21], "?", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[22], " ", RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[23], "Microsoft", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[24], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[25], "AspNetCore", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[26], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[27], "SomeTagHelpers", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[28], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[29], "AnotherTypeName", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[30], ".", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[31], "AnotherProperty", RazorPredefinedClassificationTypeNames.Identifier);
            AssertExpectedClassification(runs[32], Environment.NewLine, RazorPredefinedClassificationTypeNames.WhiteSpace);
            AssertExpectedClassification(runs[33], "Uses ", RazorPredefinedClassificationTypeNames.Text);
            AssertExpectedClassification(runs[34], "List", RazorPredefinedClassificationTypeNames.Type);
            AssertExpectedClassification(runs[35], "<", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[36], "string", RazorPredefinedClassificationTypeNames.Keyword);
            AssertExpectedClassification(runs[37], ">", RazorPredefinedClassificationTypeNames.Punctuation);
            AssertExpectedClassification(runs[38], "s", RazorPredefinedClassificationTypeNames.Text);
        }

        private static void AssertExpectedClassification(RazorClassifiedTextRun run, string expectedText, string expectedClassificationType)
        {
            Assert.Equal(expectedText, run.Text);
            Assert.Equal(expectedClassificationType, run.ClassificationTypeName);
        }
    }
}
