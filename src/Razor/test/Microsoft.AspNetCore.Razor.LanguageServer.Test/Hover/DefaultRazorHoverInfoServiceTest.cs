// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Hover;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text.Adornments;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;
using static Microsoft.AspNetCore.Razor.LanguageServer.Tooltip.DefaultVSLSPTagHelperTooltipFactory;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Hover
{
    public class DefaultRazorHoverInfoServiceTest : TagHelperServiceTestBase
    {
        internal static VSInternalClientCapabilities MarkDownCapabilities
        {
            get
            {
                return new VSInternalClientCapabilities
                {
                    TextDocument = new TextDocumentClientCapabilities
                    {
                        Hover = new HoverSetting
                        {
                            ContentFormat = new MarkupKind[] { MarkupKind.Markdown },
                        }
                    }
                };
            }
        }

        internal static VSInternalClientCapabilities PlainTextCapabilities
        {
            get
            {
                return new VSInternalClientCapabilities
                {
                    TextDocument = new TextDocumentClientCapabilities
                    {
                        Hover = new HoverSetting
                        {
                            ContentFormat = new MarkupKind[] { MarkupKind.PlainText },
                        }
                    }
                };
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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

            // Assert
            Assert.Contains("**Test1TagHelper**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            var expectedRange = new Range
            {
                Start = new Position(1, 1),
                End = new Position(1, 6),
            };
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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

            // Assert
            Assert.Contains("**Test1TagHelper**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            var expectedRange = new Range
            {
                Start = new Position(1, 9),
                End = new Position(1, 14),
            };
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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

            // Assert
            Assert.Contains("**BoolVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            Assert.DoesNotContain("**IntVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            var expectedRange = new Range
            {
                Start = new Position(1, 7),
                End = new Position(1, 15),
            };
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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

            // Assert
            Assert.Contains("**BoolVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            Assert.DoesNotContain("**IntVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            var expectedRange = new Range
            {
                Start = new Position(1, 7),
                End = new Position(1, 15),
            };
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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

            // Assert
            Assert.Contains("**BoolVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            Assert.DoesNotContain("**IntVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            var expectedRange = new Range
            {
                Start = new Position(1, 7),
                End = new Position(1, 15),
            };
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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

            // Assert
            Assert.NotNull(hover);
            Assert.Contains("**Test**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            var expectedRange = new Range
            {
                Start = new Position(1, 5),
                End = new Position(1, 10),
            };
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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

            // Assert
            Assert.Contains("**Test1TagHelper**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            var expectedRange = new Range
            {
                Start = new Position(1, 1),
                End = new Position(1, 6),
            };
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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

            // Assert
            Assert.Contains("**BoolVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            Assert.DoesNotContain("**IntVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            var expectedRange = new Range
            {
                Start = new Position(1, 7),
                End = new Position(1, 15),
            };
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

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, MarkDownCapabilities);

            // Assert
            Assert.Null(hover);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_PlainTextElement()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("test1", StringComparison.Ordinal), -1, -1);

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, PlainTextCapabilities);

            // Assert
            Assert.Contains("Test1TagHelper", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            Assert.Equal(MarkupKind.PlainText, ((MarkupContent)hover.Contents).Kind);
            var expectedRange = new Range
            {
                Start = new Position(1, 1),
                End = new Position(1, 6),
            };
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_PlainTextElement_EndTag()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.LastIndexOf("test1", StringComparison.Ordinal), -1, -1);

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, PlainTextCapabilities);

            // Assert
            Assert.Contains("Test1TagHelper", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            Assert.Equal(MarkupKind.PlainText, ((MarkupContent)hover.Contents).Kind);
            var expectedRange = new Range
            {
                Start = new Position(1, 9),
                End = new Position(1, 14),
            };
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_TagHelper_PlainTextAttribute()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 bool-val></test1>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("bool-val", StringComparison.Ordinal), -1, -1);

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, PlainTextCapabilities);

            // Assert
            Assert.Contains("BoolVal", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            Assert.DoesNotContain("IntVal", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
            Assert.Equal(MarkupKind.PlainText, ((MarkupContent)hover.Contents).Kind);
            var expectedRange = new Range
            {
                Start = new Position(1, 7),
                End = new Position(1, 15),
            };
            Assert.Equal(expectedRange, hover.Range);
        }

        [Fact]
        public void GetHoverInfo_HTML_PlainTextElement()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<p><strong></strong></p>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false);

            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("strong", StringComparison.Ordinal), -1, -1);

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, PlainTextCapabilities);

            // Assert
            Assert.Null(hover);
        }

        [Fact]
        public void GetHoverInfo_HTML_PlainTextAttribute()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<p><strong class=\"weak\"></strong></p>";
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false);

            var service = GetDefaultRazorHoverInfoService();
            var location = new SourceLocation(txt.IndexOf("weak", StringComparison.Ordinal), -1, -1);

            // Act
            var hover = service.GetHoverInfo(codeDocument, location, PlainTextCapabilities);

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
            var clientCapabilities = MarkDownCapabilities;
            clientCapabilities.SupportsVisualStudioExtensions = true;

            // Act
            var vsHover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.False(vsHover.Contents.TryGetThird(out var _));
            Assert.True(vsHover.Contents.TryGetSecond(out var _) && !vsHover.Contents.Second.Any());
            var expectedRange = new Range
            {
                Start = new Position(1, 1),
                End = new Position(1, 6),
            };
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
            var clientCapabilities = MarkDownCapabilities;
            clientCapabilities.SupportsVisualStudioExtensions = true;

            // Act
            var vsHover = service.GetHoverInfo(codeDocument, location, clientCapabilities);

            // Assert
            Assert.False(vsHover.Contents.TryGetThird(out var _));
            Assert.True(vsHover.Contents.TryGetSecond(out var markedStrings) && !markedStrings.Any());
            var expectedRange = new Range
            {
                Start = new Position(1, 7),
                End = new Position(1, 15),
            };
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
