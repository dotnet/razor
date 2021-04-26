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
            AssertExpectedClassification(runs[0], "Microsoft", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[1], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[2], "AspNetCore", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[3], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[4], "SomeTagHelper", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[5], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[6], "Uses ", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[7], "List", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[8], "<", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[9], "List", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[10], "<", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[11], "string", RazorPredefinedClassificationNames.Keyword);
            AssertExpectedClassification(runs[12], ">", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[13], ">", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[14], "s", RazorPredefinedClassificationNames.PlainText);
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
            AssertExpectedClassification(runs[0], "Microsoft", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[1], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[2], "AspNetCore", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[3], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[4], "SomeTagHelper", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[5], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[6], "Uses ", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[7], "List", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[8], "<", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[9], "string", RazorPredefinedClassificationNames.Keyword);
            AssertExpectedClassification(runs[10], ">", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[11], "s", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[12], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[13], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[14], "Microsoft", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[15], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[16], "AspNetCore", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[17], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[18], "OtherTagHelper", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[19], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[20], "Also uses ", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[21], "List", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[22], "<", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[23], "string", RazorPredefinedClassificationNames.Keyword);
            AssertExpectedClassification(runs[24], ">", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[25], "s", RazorPredefinedClassificationNames.PlainText);
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
            AssertExpectedClassification(runs[0], "string", RazorPredefinedClassificationNames.Keyword);
            AssertExpectedClassification(runs[1], " ", RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[2], "Microsoft", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[3], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[4], "AspNetCore", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[5], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[6], "SomeTagHelpers", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[7], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[8], "SomeTypeName", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[9], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[10], "SomeProperty", RazorPredefinedClassificationNames.Identifier);
            AssertExpectedClassification(runs[11], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[12], "Uses ", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[13], "List", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[14], "<", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[15], "List", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[16], "<", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[17], "string", RazorPredefinedClassificationNames.Keyword);
            AssertExpectedClassification(runs[18], ">", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[19], ">", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[20], "s", RazorPredefinedClassificationNames.PlainText);
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
            AssertExpectedClassification(runs[0], "string", RazorPredefinedClassificationNames.Keyword);
            AssertExpectedClassification(runs[1], " ", RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[2], "Microsoft", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[3], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[4], "AspNetCore", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[5], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[6], "SomeTagHelpers", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[7], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[8], "SomeTypeName", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[9], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[10], "SomeProperty", RazorPredefinedClassificationNames.Identifier);
            AssertExpectedClassification(runs[11], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[12], "Uses ", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[13], "List", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[14], "<", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[15], "string", RazorPredefinedClassificationNames.Keyword);
            AssertExpectedClassification(runs[16], ">", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[17], "s", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[18], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[19], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[20], "bool", RazorPredefinedClassificationNames.Keyword);
            AssertExpectedClassification(runs[21], "?", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[22], " ", RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[23], "Microsoft", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[24], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[25], "AspNetCore", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[26], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[27], "SomeTagHelpers", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[28], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[29], "AnotherTypeName", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[30], ".", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[31], "AnotherProperty", RazorPredefinedClassificationNames.Identifier);
            AssertExpectedClassification(runs[32], Environment.NewLine, RazorPredefinedClassificationNames.WhiteSpace);
            AssertExpectedClassification(runs[33], "Uses ", RazorPredefinedClassificationNames.PlainText);
            AssertExpectedClassification(runs[34], "List", RazorPredefinedClassificationNames.Type);
            AssertExpectedClassification(runs[35], "<", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[36], "string", RazorPredefinedClassificationNames.Keyword);
            AssertExpectedClassification(runs[37], ">", RazorPredefinedClassificationNames.Punctuation);
            AssertExpectedClassification(runs[38], "s", RazorPredefinedClassificationNames.PlainText);
        }

        private static void AssertExpectedClassification(RazorClassifiedTextRun run, string expectedText, string expectedClassificationType)
        {
            Assert.Equal(expectedText, run.Text);
            Assert.Equal(expectedClassificationType, run.ClassificationTypeName);
        }
    }
}
