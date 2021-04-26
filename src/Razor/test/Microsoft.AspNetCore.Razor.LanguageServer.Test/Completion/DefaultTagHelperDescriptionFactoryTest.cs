// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using static Microsoft.AspNetCore.Razor.LanguageServer.Tooltip.DefaultTagHelperTooltipFactory;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    public class DefaultTagHelperDescriptionFactoryTest
    {
        internal static ClientNotifierServiceBase LanguageServer
        {
            get
            {
                var initializeParams = new InitializeParams
                {
                    Capabilities = new ClientCapabilities
                    {
                        TextDocument = new TextDocumentClientCapabilities
                        {
                            Completion = new Supports<CompletionCapability>
                            {
                                Value = new CompletionCapability
                                {
                                    CompletionItem = new CompletionItemCapabilityOptions
                                    {
                                        SnippetSupport = true,
                                        DocumentationFormat = new Container<MarkupKind>(MarkupKind.Markdown)
                                    }
                                }
                            }
                        }
                    }
                };

                var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
                languageServer.SetupGet(server => server.ClientSettings)
                    .Returns(initializeParams);

                return languageServer.Object;
            }
        }

        [Fact]
        public void ReduceTypeName_Plain()
        {
            // Arrange
            var content = "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName";

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceTypeName(content);

            // Assert
            Assert.Equal("SomeTypeName", reduced);
        }

        [Fact]
        public void ReduceTypeName_Generics()
        {
            // Arrange
            var content = "System.Collections.Generic.List<System.String>";

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceTypeName(content);

            // Assert
            Assert.Equal("List<System.String>", reduced);
        }

        [Fact]
        public void ReduceTypeName_CrefGenerics()
        {
            // Arrange
            var content = "System.Collections.Generic.List{System.String}";

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceTypeName(content);

            // Assert
            Assert.Equal("List{System.String}", reduced);
        }

        [Fact]
        public void ReduceTypeName_NestedGenerics()
        {
            // Arrange
            var content = "Microsoft.AspNetCore.SometTagHelpers.SomeType<Foo.Bar<Baz.Phi>>";

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceTypeName(content);

            // Assert
            Assert.Equal("SomeType<Foo.Bar<Baz.Phi>>", reduced);
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.SometTagHelpers.SomeType.Foo.Bar<Baz.Phi>>")]
        [InlineData("Microsoft.AspNetCore.SometTagHelpers.SomeType.Foo.Bar{Baz.Phi}}")]
        public void ReduceTypeName_UnbalancedDocs_NotRecoverable_ReturnsOriginalContent(string content)
        {
            // Arrange

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceTypeName(content);

            // Assert
            Assert.Equal(content, reduced);
        }

        [Fact]
        public void ReduceMemberName_Plain()
        {
            // Arrange
            var content = "Microsoft.AspNetCore.SometTagHelpers.SomeType.SomeProperty";

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceMemberName(content);

            // Assert
            Assert.Equal("SomeType.SomeProperty", reduced);
        }

        [Fact]
        public void ReduceMemberName_Generics()
        {
            // Arrange
            var content = "Microsoft.AspNetCore.SometTagHelpers.SomeType<Foo.Bar>.SomeProperty<Foo.Bar>";

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceMemberName(content);

            // Assert
            Assert.Equal("SomeType<Foo.Bar>.SomeProperty<Foo.Bar>", reduced);
        }

        [Fact]
        public void ReduceMemberName_CrefGenerics()
        {
            // Arrange
            var content = "Microsoft.AspNetCore.SometTagHelpers.SomeType{Foo.Bar}.SomeProperty{Foo.Bar}";

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceMemberName(content);

            // Assert
            Assert.Equal("SomeType{Foo.Bar}.SomeProperty{Foo.Bar}", reduced);
        }

        [Fact]
        public void ReduceMemberName_NestedGenericsMethodsTypes()
        {
            // Arrange
            var content = "Microsoft.AspNetCore.SometTagHelpers.SomeType<Foo.Bar<Baz,Fi>>.SomeMethod(Foo.Bar<System.String>,Baz<Something>.Fi)";

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceMemberName(content);

            // Assert
            Assert.Equal("SomeType<Foo.Bar<Baz,Fi>>.SomeMethod(Foo.Bar<System.String>,Baz<Something>.Fi)", reduced);
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.SometTagHelpers.SomeType.Foo.Bar<Baz.Phi>>")]
        [InlineData("Microsoft.AspNetCore.SometTagHelpers.SomeType.Foo.Bar{Baz.Phi}}")]
        [InlineData("Microsoft.AspNetCore.SometTagHelpers.SomeType.Foo.Bar(Baz.Phi))")]
        [InlineData("Microsoft.AspNetCore.SometTagHelpers.SomeType.Foo{.>")]
        public void ReduceMemberName_UnbalancedDocs_NotRecoverable_ReturnsOriginalContent(string content)
        {
            // Arrange

            // Act
            var reduced = DefaultTagHelperTooltipFactory.ReduceMemberName(content);

            // Assert
            Assert.Equal(content, reduced);
        }

        [Fact]
        public void ReduceCrefValue_InvalidShortValue_ReturnsEmptyString()
        {
            // Arrange
            var content = "T:";

            // Act
            var value = DefaultTagHelperTooltipFactory.ReduceCrefValue(content);

            // Assert
            Assert.Equal(string.Empty, value);
        }

        [Fact]
        public void ReduceCrefValue_InvalidUnknownIdentifierValue_ReturnsEmptyString()
        {
            // Arrange
            var content = "X:";

            // Act
            var value = DefaultTagHelperTooltipFactory.ReduceCrefValue(content);

            // Assert
            Assert.Equal(string.Empty, value);
        }

        [Fact]
        public void ReduceCrefValue_Type()
        {
            // Arrange
            var content = "T:Microsoft.AspNetCore.SometTagHelpers.SomeType";

            // Act
            var value = DefaultTagHelperTooltipFactory.ReduceCrefValue(content);

            // Assert
            Assert.Equal("SomeType", value);
        }

        [Fact]
        public void ReduceCrefValue_Property()
        {
            // Arrange
            var content = "P:Microsoft.AspNetCore.SometTagHelpers.SomeType.SomeProperty";

            // Act
            var value = DefaultTagHelperTooltipFactory.ReduceCrefValue(content);

            // Assert
            Assert.Equal("SomeType.SomeProperty", value);
        }

        [Fact]
        public void ReduceCrefValue_Member()
        {
            // Arrange
            var content = "P:Microsoft.AspNetCore.SometTagHelpers.SomeType.SomeMember";

            // Act
            var value = DefaultTagHelperTooltipFactory.ReduceCrefValue(content);

            // Assert
            Assert.Equal("SomeType.SomeMember", value);
        }

        [Fact]
        public void TryExtractSummary_Null_ReturnsFalse()
        {
            // Arrange & Act
            var result = DefaultTagHelperTooltipFactory.TryExtractSummary(documentation: null, out var summary);

            // Assert
            Assert.False(result);
            Assert.Null(summary);
        }

        [Fact]
        public void TryExtractSummary_ExtractsSummary_ReturnsTrue()
        {
            // Arrange
            var expectedSummary = " Hello World ";
            var documentation = $@"
Prefixed invalid content


<summary>{expectedSummary}</summary>

Suffixed invalid content";

            // Act
            var result = DefaultTagHelperTooltipFactory.TryExtractSummary(documentation, out var summary);

            // Assert
            Assert.True(result);
            Assert.Equal(expectedSummary, summary);
        }

        [Fact]
        public void TryExtractSummary_NoStartSummary_ReturnsFalse()
        {
            // Arrange
            var documentation = @"
Prefixed invalid content


</summary>

Suffixed invalid content";

            // Act
            var result = DefaultTagHelperTooltipFactory.TryExtractSummary(documentation, out var summary);

            // Assert
            Assert.True(result);
            Assert.Equal(@"Prefixed invalid content


</summary>

Suffixed invalid content", summary);
        }

        [Fact]
        public void TryExtractSummary_NoEndSummary_ReturnsTrue()
        {
            // Arrange
            var documentation = @"
Prefixed invalid content


<summary>

Suffixed invalid content";

            // Act
            var result = DefaultTagHelperTooltipFactory.TryExtractSummary(documentation, out var summary);

            // Assert
            Assert.True(result);
            Assert.Equal(@"Prefixed invalid content


<summary>

Suffixed invalid content", summary);
        }

        [Fact]
        public void TryExtractSummary_XMLButNoSummary_ReturnsFalse()
        {
            // Arrange
            var documentation = @"
<param type=""stuff"">param1</param>
<return>Result</return>
";

            // Act
            var result = DefaultTagHelperTooltipFactory.TryExtractSummary(documentation, out var summary);

            // Assert
            Assert.False(result);
            Assert.Null(summary);
        }

        [Fact]
        public void TryExtractSummary_NoXml_ReturnsTrue()
        {
            // Arrange
            var documentation = @"
There is no xml, but I got you this < and the >.
";

            // Act
            var result = DefaultTagHelperTooltipFactory.TryExtractSummary(documentation, out var summary);

            // Assert
            Assert.True(result);
            Assert.Equal("There is no xml, but I got you this < and the >.", summary);
        }

        [Fact]
        public void CleanSummaryContent_ReplacesSeeCrefs()
        {
            // Arrange
            var summary = "Accepts <see cref=\"T:System.Collections.List{System.String}\" />s";

            // Act
            var cleanedSummary = DefaultTagHelperTooltipFactory.CleanSummaryContent(summary);

            // Assert
            Assert.Equal("Accepts `List<System.String>`s", cleanedSummary);
        }

        [Fact]
        public void CleanSummaryContent_ReplacesSeeAlsoCrefs()
        {
            // Arrange
            var summary = "Accepts <seealso cref=\"T:System.Collections.List{System.String}\" />s";

            // Act
            var cleanedSummary = DefaultTagHelperTooltipFactory.CleanSummaryContent(summary);

            // Assert
            Assert.Equal("Accepts `List<System.String>`s", cleanedSummary);
        }

        [Fact]
        public void CleanSummaryContent_TrimsSurroundingWhitespace()
        {
            // Arrange
            var summary = @"
            Hello

    World

";

            // Act
            var cleanedSummary = DefaultTagHelperTooltipFactory.CleanSummaryContent(summary);

            // Assert
            Assert.Equal(@"Hello

World", cleanedSummary);
        }

        [Fact]
        public void TryCreateTooltip_NoAssociatedTagHelperDescriptions_ReturnsFalse()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(LanguageServer);
            var elementDescription = AggregateBoundElementDescription.Default;

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out MarkupContent markdown);

            // Assert
            Assert.False(result);
            Assert.Null(markdown);
        }

        [Fact]
        public void TryCreateTooltip_Element_SingleAssociatedTagHelper_ReturnsTrue()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(LanguageServer);
            var associatedTagHelperInfos = new[]
            {
                new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>Uses <see cref=\"T:System.Collections.List{System.String}\" />s</summary>"),
            };
            var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos);
            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out MarkupContent markdown);

            // Assert
            Assert.True(result);
            Assert.Equal(@"**SomeTagHelper**

Uses `List<System.String>`s", markdown.Value);
            Assert.Equal(MarkupKind.Markdown, markdown.Kind);
        }

        [Fact]
        public void TryCreateTooltip_Element_PlainText_NoBold()
        {
            // Arrange
            var languageServer = LanguageServer;
            languageServer.ClientSettings.Capabilities.TextDocument.Completion.Value.CompletionItem.DocumentationFormat = new Container<MarkupKind>(MarkupKind.PlainText);
            var descriptionFactory = new DefaultTagHelperTooltipFactory(languageServer);
            var associatedTagHelperInfos = new[]
            {
                new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>Uses <see cref=\"T:System.Collections.List{System.String}\" />s</summary>"),
            };
            var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos);

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out MarkupContent markdown);

            // Assert
            Assert.True(result, "TryCreateTooltip should have succeeded");
            Assert.Equal(@"SomeTagHelper

Uses `List<System.String>`s", markdown.Value);
            Assert.Equal(MarkupKind.PlainText, markdown.Kind);
        }

        [Fact]
        public void TryCreateTooltip_Attribute_PlainText_NoBold()
        {
            // Arrange
            var languageServer = LanguageServer;
            languageServer.ClientSettings.Capabilities.TextDocument.Completion.Value.CompletionItem.DocumentationFormat = new Container<MarkupKind>(MarkupKind.PlainText);
            var descriptionFactory = new DefaultTagHelperTooltipFactory(languageServer);
            var associatedAttributeDescriptions = new[]
            {
                new BoundAttributeDescriptionInfo(
                    returnTypeName: "System.String",
                    typeName: "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName",
                    propertyName: "SomeProperty",
                    documentation: "<summary>Uses <see cref=\"T:System.Collections.List{System.String}\" />s</summary>")
            };
            var attributeDescription = new AggregateBoundAttributeDescription(associatedAttributeDescriptions);

            // Act
            var result = descriptionFactory.TryCreateTooltip(attributeDescription, out MarkupContent markdown);

            // Assert
            Assert.True(result);
            Assert.Equal(@"string SomeTypeName.SomeProperty

Uses `List<System.String>`s", markdown.Value);
            Assert.Equal(MarkupKind.PlainText, markdown.Kind);
        }
        [Fact]
        public void TryCreateTooltip_Element_BothPlainTextAndMarkdown_IsBold()
        {
            // Arrange
            var languageServer = LanguageServer;
            languageServer.ClientSettings.Capabilities.TextDocument.Completion.Value.CompletionItem.DocumentationFormat = new Container<MarkupKind>(MarkupKind.PlainText, MarkupKind.Markdown);
            var descriptionFactory = new DefaultTagHelperTooltipFactory(languageServer);
            var associatedTagHelperInfos = new[]
            {
                new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>Uses <see cref=\"T:System.Collections.List{System.String}\" />s</summary>"),
            };
            var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos);

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out MarkupContent markdown);

            // Assert
            Assert.True(result);
            Assert.Equal(@"**SomeTagHelper**

Uses `List<System.String>`s", markdown.Value);
            Assert.Equal(MarkupKind.Markdown, markdown.Kind);
        }

        [Fact]
        public void TryCreateTooltip_Element_MultipleAssociatedTagHelpers_ReturnsTrue()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(LanguageServer);
            var associatedTagHelperInfos = new[]
            {
                new BoundElementDescriptionInfo("Microsoft.AspNetCore.SomeTagHelper", "<summary>\nUses <see cref=\"T:System.Collections.List{System.String}\" />s\n</summary>"),
                new BoundElementDescriptionInfo("Microsoft.AspNetCore.OtherTagHelper", "<summary>\nAlso uses <see cref=\"T:System.Collections.List{System.String}\" />s\n\r\n\r\r</summary>"),
            };
            var elementDescription = new AggregateBoundElementDescription(associatedTagHelperInfos);

            // Act
            var result = descriptionFactory.TryCreateTooltip(elementDescription, out MarkupContent markdown);

            // Assert
            Assert.True(result);
            Assert.Equal(@"**SomeTagHelper**

Uses `List<System.String>`s
---
**OtherTagHelper**

Also uses `List<System.String>`s", markdown.Value);
            Assert.Equal(MarkupKind.Markdown, markdown.Kind);
        }

        [Fact]
        public void TryCreateTooltip_Attribute_SingleAssociatedAttribute_ReturnsTrue()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(LanguageServer);
            var associatedAttributeDescriptions = new[]
            {
                new BoundAttributeDescriptionInfo(
                    returnTypeName: "System.String",
                    typeName: "Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName",
                    propertyName: "SomeProperty",
                    documentation: "<summary>Uses <see cref=\"T:System.Collections.List{System.String}\" />s</summary>")
            };
            var attributeDescription = new AggregateBoundAttributeDescription(associatedAttributeDescriptions);

            // Act
            var result = descriptionFactory.TryCreateTooltip(attributeDescription, out MarkupContent markdown);

            // Assert
            Assert.True(result);
            Assert.Equal(@"**string** SomeTypeName.**SomeProperty**

Uses `List<System.String>`s", markdown.Value);
            Assert.Equal(MarkupKind.Markdown, markdown.Kind);
        }

        [Fact]
        public void TryCreateTooltip_Attribute_MultipleAssociatedAttributes_ReturnsTrue()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(LanguageServer);
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
            var result = descriptionFactory.TryCreateTooltip(attributeDescription, out MarkupContent markdown);

            // Assert
            Assert.True(result);
            Assert.Equal(@"**string** SomeTypeName.**SomeProperty**

Uses `List<System.String>`s
---
**Boolean?** AnotherTypeName.**AnotherProperty**

Uses `List<System.String>`s", markdown.Value);
            Assert.Equal(MarkupKind.Markdown, markdown.Kind);
        }

        [Fact]
        public void TryCreateTooltip_ClassifiedTextElement_Element_SingleAssociatedTagHelper_ReturnsTrue_NestedTypes()
        {
            // Arrange
            var descriptionFactory = new DefaultTagHelperTooltipFactory(LanguageServer);
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

            // Expected output:
            //     Microsoft.AspNetCore.SomeTagHelper
            //     Uses List<List<string>>s
            var runs = classifiedTextElement.Runs.ToArray();
            Assert.True(runs.Length == 15, $"Number of runs does not match. Expected: {15} Actual: {runs.Length}");

            Assert.Equal("Microsoft", runs[0].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[0].ClassificationTypeName);

            Assert.Equal(".", runs[1].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[1].ClassificationTypeName);

            Assert.Equal("AspNetCore", runs[2].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[2].ClassificationTypeName);

            Assert.Equal(".", runs[3].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[3].ClassificationTypeName);

            Assert.Equal("SomeTagHelper", runs[4].Text);
            Assert.Equal(RazorPredefinedClassificationNames.Type, runs[4].ClassificationTypeName);

            Assert.Equal(Environment.NewLine, runs[5].Text);
            Assert.Equal(RazorPredefinedClassificationNames.WhiteSpace, runs[5].ClassificationTypeName);

            Assert.Equal("Uses ", runs[6].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[6].ClassificationTypeName);

            Assert.Equal("List", runs[7].Text);
            Assert.Equal(RazorPredefinedClassificationNames.Type, runs[7].ClassificationTypeName);

            Assert.Equal("<", runs[8].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[8].ClassificationTypeName);

            Assert.Equal("List", runs[9].Text);
            Assert.Equal(RazorPredefinedClassificationNames.Type, runs[9].ClassificationTypeName);

            Assert.Equal("<", runs[10].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[10].ClassificationTypeName);

            Assert.Equal("string", runs[11].Text);
            Assert.Equal(RazorPredefinedClassificationNames.Keyword, runs[11].ClassificationTypeName);

            Assert.Equal(">", runs[12].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[12].ClassificationTypeName);

            Assert.Equal(">", runs[13].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[13].ClassificationTypeName);

            Assert.Equal("s", runs[14].Text);
            Assert.Equal(RazorPredefinedClassificationNames.PlainText, runs[14].ClassificationTypeName);
        }
    }
}
