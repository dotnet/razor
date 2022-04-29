// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Hover;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.VisualStudio.Text.Adornments;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using static Microsoft.AspNetCore.Razor.LanguageServer.Tooltip.DefaultVSLSPTagHelperTooltipFactory;
using RangeModel = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Hover
{
    public class DefaultRazorHoverInfoServiceTest : TagHelperServiceTestBase
    {
        internal static ClientNotifierServiceBase LanguageServer
        {
            get
            {
                var initializeParams = new InitializeParams
                {
                    Capabilities = new PlatformAgnosticClientCapabilities
                    {
                        TextDocument = new TextDocumentClientCapabilities
                        {
                            Hover = new Supports<HoverCapability>
                            {
                                Value = new HoverCapability
                                {
                                    ContentFormat = new Container<MarkupKind>(MarkupKind.Markdown)
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
        public void GetHoverInfo_TagHelper_Element()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("test1", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("**Test1TagHelper**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            var expectedRange = new RangeModel(new Position(1, 1), new Position(1, 6));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_Element_EndTag()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.LastIndexOf("test1", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("**Test1TagHelper**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            var expectedRange = new RangeModel(new Position(1, 9), new Position(1, 14));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_Attribute()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true'></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("bool-val", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("**BoolVal**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("**IntVal**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            var expectedRange = new RangeModel(new Position(1, 7), new Position(1, 15));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_AttributeTrailingEdge()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val minimized></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var edgeLocation = txt.IndexOf("bool-val", StringComparison.Ordinal) + "bool-val".Length;
            var location = new SourceLocation(edgeLocation, 0, edgeLocation);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("**BoolVal**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("**IntVal**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            var expectedRange = new RangeModel(new Position(1, 7), new Position(1, 15));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_AttributeValue_ReturnsNull()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true'></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("true", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Null(hover);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_AfterAttributeEquals_ReturnsNull()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true'></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("=", StringComparison.Ordinal) + 1, -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Null(hover);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_AttributeEnd_ReturnsNull()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true'></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("true'", StringComparison.Ordinal) + 5, -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Null(hover);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_MinimizedAttribute()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("bool-val", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("**BoolVal**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("**IntVal**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            var expectedRange = new RangeModel(new Position(1, 7), new Position(1, 15));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_DirectiveAttribute_HasResult()
        {
            // Arrange
            var txt = @"@addTagHelper *, TestAssembly
<any @test=""Increment"" />
@code{
    public void Increment(){
    }
}";
            var codeDocument = CreateCodeDocument(txt, "text.razor", DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var charIndex = txt.IndexOf("@test", StringComparison.Ordinal) + 2;
            var location = new SourceLocation(charIndex, -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.NotNull(hover);
            Assert.Contains("**Test**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            var expectedRange = new RangeModel(new Position(1, 5), new Position(1, 10));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_MalformedElement()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1<hello";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("test1", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("**Test1TagHelper**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            var expectedRange = new RangeModel(new Position(1, 1), new Position(1, 6));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_MalformedAttribute()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val=\"aslj alsk<strong>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("bool-val", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("**BoolVal**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("**IntVal**", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            var expectedRange = new RangeModel(new Position(1, 7), new Position(1, 15));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_HTML_MarkupElement()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<p><strong></strong></p>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("strong", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Null(hover);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_PlainTextElement()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

            var languageServer = LanguageServer;
            languageServer.ClientSettings.Capabilities.TextDocument.Hover.Value.ContentFormat = new Container<MarkupKind>(MarkupKind.PlainText);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("test1", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = languageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("Test1TagHelper", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            Assert.Equal(MarkupKind.PlainText, hover.Contents.MarkupContent.Kind);
            var expectedRange = new RangeModel(new Position(1, 1), new Position(1, 6));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_PlainTextElement_EndTag()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

            var languageServer = LanguageServer;
            languageServer.ClientSettings.Capabilities.TextDocument.Hover.Value.ContentFormat = new Container<MarkupKind>(MarkupKind.PlainText);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.LastIndexOf("test1", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = languageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("Test1TagHelper", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            Assert.Equal(MarkupKind.PlainText, hover.Contents.MarkupContent.Kind);
            var expectedRange = new RangeModel(new Position(1, 9), new Position(1, 14));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_PlainTextAttribute()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

            var languageServer = LanguageServer;
            languageServer.ClientSettings.Capabilities.TextDocument.Hover.Value.ContentFormat = new Container<MarkupKind>(MarkupKind.PlainText);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("bool-val", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = languageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Contains("BoolVal", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("IntVal", hover.Contents.MarkupContent.Value, StringComparison.Ordinal);
            Assert.Equal(MarkupKind.PlainText, hover.Contents.MarkupContent.Kind);
            var expectedRange = new RangeModel(new Position(1, 7), new Position(1, 15));
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_HTML_PlainTextElement()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<p><strong></strong></p>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false);

            var languageServer = LanguageServer;
            languageServer.ClientSettings.Capabilities.TextDocument.Hover.Value.ContentFormat = new Container<MarkupKind>(MarkupKind.PlainText);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("strong", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = languageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Null(hover);
        }

        [Fact]
        public void GetHoverInfo_HTML_PlainTextAttribute()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<p><strong class=\"weak\"></strong></p>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false);

            var languageServer = LanguageServer;
            languageServer.ClientSettings.Capabilities.TextDocument.Hover.Value.ContentFormat = new Container<MarkupKind>(MarkupKind.PlainText);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("weak", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = languageServer.ClientSettings.Capabilities;

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.Null(hover);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_Element_VSClient_ReturnVSHover()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("test1", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;
            ((PlatformAgnosticClientCapabilities)clientCapabilities).SupportsVisualStudioExtensions = true;

            // Act
            var vsHover = (OmniSharpVSHover)service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.False(vsHover.Contents.HasMarkupContent);
            Assert.True(vsHover.Contents.HasMarkedStrings && !vsHover.Contents.MarkedStrings.Any());
            var expectedRange = new RangeModel(new Position(1, 1), new Position(1, 6));
            Assert.Equal(expectedRange, vsHover.Range);

            var container = (ContainerElement)vsHover.RawContent;
            var containerElements = container.Elements.ToList();
            Assert.Equal(ContainerElementStyle.Stacked, container.Style);
            Assert.Single(containerElements);

            // [TagHelper Glyph] Test1TagHelper
            var innerContainer = ((ContainerElement)containerElements[0]).Elements.ToList();
            var classifiedTextElement = (ClassifiedTextElement)innerContainer[1];
            Assert.Equal(2, innerContainer.Count);
            Assert.Equal(ClassGlyph, innerContainer[0]);
            Assert.Collection(classifiedTextElement.Runs,
                run => DefaultVSLSPTagHelperTooltipFactoryTest.AssertExpectedClassification(run, "Test1TagHelper", VSPredefinedClassificationTypeNames.Type));
        }

        [Fact]
        public void GetHoverInfo_TagHelper_Attribute_VSClient_ReturnVSHover()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val='true'></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("bool-val", StringComparison.Ordinal), -1, -1);
            var clientCapabilities = LanguageServer.ClientSettings.Capabilities;
            ((PlatformAgnosticClientCapabilities)clientCapabilities).SupportsVisualStudioExtensions = true;

            // Act
            var vsHover = (OmniSharpVSHover)service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.False(vsHover.Contents.HasMarkupContent);
            Assert.True(vsHover.Contents.HasMarkedStrings && !vsHover.Contents.MarkedStrings.Any());
            var expectedRange = new RangeModel(new Position(1, 7), new Position(1, 15));
            Assert.Equal(expectedRange, vsHover.Range);

            var container = (ContainerElement)vsHover.RawContent;
            var containerElements = container.Elements.ToList();
            Assert.Equal(ContainerElementStyle.Stacked, container.Style);
            Assert.Single(containerElements);

            // [TagHelper Glyph] bool Test1TagHelper.BoolVal
            var innerContainer = ((ContainerElement)containerElements[0]).Elements.ToList();
            var classifiedTextElement = (ClassifiedTextElement)innerContainer[1];
            Assert.Equal(2, innerContainer.Count);
            Assert.Equal(PropertyGlyph, innerContainer[0]);
            Assert.Collection(classifiedTextElement.Runs,
                run => DefaultVSLSPTagHelperTooltipFactoryTest.AssertExpectedClassification(run, "bool", VSPredefinedClassificationTypeNames.Keyword),
                run => DefaultVSLSPTagHelperTooltipFactoryTest.AssertExpectedClassification(run, " ", VSPredefinedClassificationTypeNames.WhiteSpace),
                run => DefaultVSLSPTagHelperTooltipFactoryTest.AssertExpectedClassification(run, "Test1TagHelper", VSPredefinedClassificationTypeNames.Type),
                run => DefaultVSLSPTagHelperTooltipFactoryTest.AssertExpectedClassification(run, ".", VSPredefinedClassificationTypeNames.Punctuation),
                run => DefaultVSLSPTagHelperTooltipFactoryTest.AssertExpectedClassification(run, "BoolVal", VSPredefinedClassificationTypeNames.Identifier));
        }

        private DefaultRazorHoverInfoService GetDefaultRazorHoverInfoService()
        {
            var lspTagHelperTooltipFactory = new DefaultLSPTagHelperTooltipFactory();
            var vsLspTagHelperTooltipFactory = new DefaultVSLSPTagHelperTooltipFactory();
            return new DefaultRazorHoverInfoService(TagHelperFactsService, lspTagHelperTooltipFactory, vsLspTagHelperTooltipFactory, HtmlFactsService);
        }
    }
}
