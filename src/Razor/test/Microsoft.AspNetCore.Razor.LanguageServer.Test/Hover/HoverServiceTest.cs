﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.LanguageServer.Hover;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text.Adornments;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.AspNetCore.Razor.LanguageServer.Tooltip.DefaultVSLSPTagHelperTooltipFactory;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Hover;

[UseExportProvider]
public class HoverServiceTest(ITestOutputHelper testOutput) : TagHelperServiceTestBase(testOutput)
{
    private static VSInternalClientCapabilities CreateMarkDownCapabilities()
        => CreateCapabilities(MarkupKind.Markdown);

    private static VSInternalClientCapabilities CreatePlainTextCapabilities()
        => CreateCapabilities(MarkupKind.PlainText);

    private static VSInternalClientCapabilities CreateCapabilities(MarkupKind markupKind)
        => new()
        {
            TextDocument = new()
            {
                Hover = new()
                {
                    ContentFormat = [markupKind],
                }
            }
        };

    [Fact]
    public async Task GetHoverInfo_TagHelper_Element()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <$$test1></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("**Test1TagHelper**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 1, length: 5);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_Element_WithParent()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1>
                    <Som$$eChild></SomeChild>
                </test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("**SomeChild**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 2, character: 5, length: 9);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_Attribute_WithParent()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1>
                    <SomeChild [|att$$ribute|]="test"></SomeChild>
                </test1>
                """;
        TestFileMarkupParser.GetPositionAndSpan(txt, out txt, out var cursorPosition, out var span);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("**Attribute**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = codeDocument.Source.Text.GetRange(span);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_Element_EndTag()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1></$$test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("**Test1TagHelper**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 9, length: 5);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_Attribute()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1 $$bool-val='true'></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("**BoolVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.DoesNotContain("**IntVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 7, length: 8);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_AttributeTrailingEdge()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1 bool-val$$ minimized></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var edgeLocation = cursorPosition;
        var location = new SourceLocation(edgeLocation, 0, edgeLocation);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("**BoolVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.DoesNotContain("**IntVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 7, length: 8);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_AttributeValue_ReturnsNull()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1 bool-val='$$true'></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Null(hover);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_AfterAttributeEquals_ReturnsNull()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1 bool-val=$$'true'></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Null(hover);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_AttributeEnd_ReturnsNull()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1 bool-val='true'$$></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Null(hover);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_MinimizedAttribute()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1 $$bool-val></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("**BoolVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.DoesNotContain("**IntVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 7, length: 8);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_DirectiveAttribute_HasResult()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <any @t$$est="Increment" />
                @code{
                    public void Increment(){
                    }
                }
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, "text.razor", DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.NotNull(hover);
        Assert.Contains("**Test**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 5, length: 5);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_MalformedElement()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <$$test1<hello
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("**Test1TagHelper**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 1, length: 5);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_MalformedAttribute()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1 $$bool-val=\"aslj alsk<strong>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("**BoolVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.DoesNotContain("**IntVal**", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 7, length: 8);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_HTML_MarkupElement()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <p><$$strong></strong></p>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreateMarkDownCapabilities(), DisposalToken);

        // Assert
        Assert.Null(hover);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_PlainTextElement()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <$$test1></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

        var service =  GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreatePlainTextCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("Test1TagHelper", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.Equal(MarkupKind.PlainText, ((MarkupContent)hover.Contents).Kind);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 1, length: 5);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_PlainTextElement_EndTag()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1></$$test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreatePlainTextCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("Test1TagHelper", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.Equal(MarkupKind.PlainText, ((MarkupContent)hover.Contents).Kind);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 9, length: 5);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_TextComponent()
    {
        // Arrange
        var txt = """
                <$$Text></Text>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: true, DefaultTagHelpers);

        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.razor", codeDocument, location, CreatePlainTextCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("Text", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.Equal(MarkupKind.PlainText, ((MarkupContent)hover.Contents).Kind);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 0, character: 1, length: 4);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_TextComponent_NestedInHtml()
    {
        // Arrange
        var txt = """
                <div>
                    <$$Text></Text>
                </div>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: true, DefaultTagHelpers);

        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.razor", codeDocument, location, CreatePlainTextCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("Text", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.Equal(MarkupKind.PlainText, ((MarkupContent)hover.Contents).Kind);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 5, length: 4);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_TextComponent_NestedInCSharp()
    {
        // Arrange
        var txt = """
                @if (true)
                {
                    <$$Text></Text>
                }
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: true, DefaultTagHelpers);

        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.razor", codeDocument, location, CreatePlainTextCapabilities(), DisposalToken);

        // Assert
        Assert.Null(hover);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_TextComponent_NestedInCSharpAndText()
    {
        // Arrange
        var txt = """
                @if (true)
                {
                    <text>
                        <$$Text></Text>
                    </text>
                }
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: true, DefaultTagHelpers);

        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.razor", codeDocument, location, CreatePlainTextCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("Text", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.Equal(MarkupKind.PlainText, ((MarkupContent)hover.Contents).Kind);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 3, character: 9, length: 4);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_PlainTextAttribute()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1 $$bool-val></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreatePlainTextCapabilities(), DisposalToken);

        // Assert
        Assert.Contains("BoolVal", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.DoesNotContain("IntVal", ((MarkupContent)hover.Contents).Value, StringComparison.Ordinal);
        Assert.Equal(MarkupKind.PlainText, ((MarkupContent)hover.Contents).Kind);
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 7, length: 8);
        Assert.Equal(expectedRange, hover.Range);
    }

    [Fact]
    public async Task GetHoverInfo_HTML_PlainTextElement()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <p><$$strong></strong></p>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false);

        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreatePlainTextCapabilities(), DisposalToken);

        // Assert
        Assert.Null(hover);
    }

    [Fact]
    public async Task GetHoverInfo_HTML_PlainTextAttribute()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <p><strong class="$$weak"></strong></p>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false);

        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();

        var location = new SourceLocation(cursorPosition, -1, -1);

        // Act
        var hover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, CreatePlainTextCapabilities(), DisposalToken);

        // Assert
        Assert.Null(hover);
    }

    [Fact]
    public async Task GetHoverInfo_TagHelper_Element_VSClient_ReturnVSHover()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <$$test1></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);
        var clientCapabilities = CreateMarkDownCapabilities();
        clientCapabilities.SupportsVisualStudioExtensions = true;

        // Act
        var vsHover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, clientCapabilities, DisposalToken);

        // Assert
        Assert.False(vsHover.Contents.Value.TryGetFourth(out var _));
        Assert.True(vsHover.Contents.Value.TryGetThird(out var _) && !vsHover.Contents.Value.Third.Any());
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 1, length: 5);
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
    public async Task GetHoverInfo_TagHelper_Attribute_VSClient_ReturnVSHover()
    {
        // Arrange
        var txt = """
                @addTagHelper *, TestAssembly
                <test1 $$bool-val='true'></test1>
                """;
        TestFileMarkupParser.GetPosition(txt, out txt, out var cursorPosition);

        var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);

        var service = GetHoverService();
        var serviceAccessor = service.GetTestAccessor();
        var location = new SourceLocation(cursorPosition, -1, -1);
        var clientCapabilities = CreateMarkDownCapabilities();
        clientCapabilities.SupportsVisualStudioExtensions = true;

        // Act
        var vsHover = await serviceAccessor.GetHoverInfoAsync("file.cshtml", codeDocument, location, clientCapabilities, DisposalToken);

        // Assert
        Assert.False(vsHover.Contents.Value.TryGetFourth(out var _));
        Assert.True(vsHover.Contents.Value.TryGetThird(out var markedStrings) && !markedStrings.Any());
        var expectedRange = VsLspFactory.CreateSingleLineRange(line: 1, character: 7, length: 8);
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

    [Fact]
    public async Task Handle_Hover_SingleServer_CallsDelegatedLanguageServer()
    {
        // Arrange
        var languageServerFeatureOptions = Mock.Of<LanguageServerFeatureOptions>(options => options.SingleServerSupport == true && options.UseRazorCohostServer == false, MockBehavior.Strict);

        var delegatedHover = new VSInternalHover();

        var clientConnectionMock = new Mock<IClientConnection>(MockBehavior.Strict);
        clientConnectionMock
            .Setup(c => c.SendRequestAsync<IDelegatedParams, VSInternalHover>(CustomMessageNames.RazorHoverEndpointName, It.IsAny<DelegatedPositionParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(delegatedHover);

        var documentMappingServiceMock = new Mock<IDocumentMappingService>(MockBehavior.Strict);
        documentMappingServiceMock
            .Setup(c => c.GetLanguageKind(It.IsAny<RazorCodeDocument>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(RazorLanguageKind.CSharp);

        var outRange = new LinePositionSpan();
        documentMappingServiceMock
            .Setup(c => c.TryMapToGeneratedDocumentRange(It.IsAny<IRazorGeneratedDocument>(), It.IsAny<LinePositionSpan>(), out outRange))
            .Returns(true);

        var projectedPosition = new LinePosition(1, 1);
        var projectedIndex = 1;
        documentMappingServiceMock.Setup(
            c => c.TryMapToGeneratedDocumentPosition(It.IsAny<IRazorGeneratedDocument>(), It.IsAny<int>(), out projectedPosition, out projectedIndex))
            .Returns(true);

        var endpoint = CreateEndpoint(languageServerFeatureOptions, documentMappingServiceMock.Object, clientConnectionMock.Object);

        var request = new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = new Uri("C:/text.razor")
            },
            Position = VsLspFactory.CreatePosition(1, 0),
        };
        var documentContext = CreateDefaultDocumentContext();
        var requestContext = CreateRazorRequestContext(documentContext: documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Same(delegatedHover, result);
    }

    [Fact]
    public async Task Handle_Hover_SingleServer_CSharpVariable()
    {
        // Arrange
        var input = """
                <div></div>

                @{
                    var $$myVariable = "Hello";

                    var length = myVariable.Length;
                }
                """;

        // Act
        var result = await GetResultFromSingleServerEndpointAsync(input);

        // Assert
        var range = result.Range;
        var expected = VsLspFactory.CreateSingleLineRange(line: 3, character: 8, length: 10);

        Assert.Equal(expected, range);

        var rawContainer = (ContainerElement)result.RawContent;
        var embeddedContainerElement = (ContainerElement)rawContainer.Elements.Single();

        var classifiedText = (ClassifiedTextElement)embeddedContainerElement.Elements.ElementAt(1);
        var text = string.Join("", classifiedText.Runs.Select(r => r.Text));

        // No need to validate exact text, in case the c# language server changes. Just
        // verify that the expected variable is represented within the hover text
        Assert.Contains("myVariable", text);
    }

    [Fact]
    public async Task Handle_Hover_SingleServer_Component()
    {
        // Arrange
        var input = """
                @addTagHelper *, TestAssembly

                <$$test1></test1>
                """;

        // Act
        var result = await GetResultFromSingleServerEndpointAsync(input);

        // Assert
        var range = result.Range;
        var expected = VsLspFactory.CreateSingleLineRange(line: 2, character: 1, length: 5);

        Assert.Equal(expected, range);

        var rawContainer = (ContainerElement)result.RawContent;
        var embeddedContainerElement = (ContainerElement)rawContainer.Elements.Single();

        var classifiedText = (ClassifiedTextElement)embeddedContainerElement.Elements.ElementAt(1);
        var text = string.Join("", classifiedText.Runs.Select(r => r.Text));
        Assert.Equal("Test1TagHelper", text);
    }

    [Fact]
    public async Task Handle_Hover_SingleServer_AddTagHelper()
    {
        // Arrange
        var input = """
            @addTagHelper *, Test$$Assembly

            <test1></test1>
            """;

        // Act
        var result = await GetResultFromSingleServerEndpointAsync(input);

        // Assert

        // Roslyn returns us a range that is outside of our source mappings, so we expect the endpoint
        // to return null, so as not to confuse the client
        Assert.Null(result.Range);

        var rawContainer = (ContainerElement)result.RawContent;
        var embeddedContainerElement = (ContainerElement)rawContainer.Elements.Single();

        if (embeddedContainerElement.Elements.FirstOrDefault() is ContainerElement headerContainer)
        {
            embeddedContainerElement = headerContainer;
        }

        var classifiedText = (ClassifiedTextElement)embeddedContainerElement.Elements.ElementAt(1);
        var text = string.Join("", classifiedText.Runs.Select(r => r.Text));
        // Hover info is for a string
        Assert.StartsWith("class System.String", text);
    }

    private async Task<VSInternalHover> GetResultFromSingleServerEndpointAsync(string input)
    {
        TestFileMarkupParser.GetPosition(input, out var output, out var cursorPosition);
        var codeDocument = CreateCodeDocument(output, DefaultTagHelpers);
        var csharpSourceText = codeDocument.GetCSharpSourceText();
        var csharpDocumentUri = new Uri("C:/path/to/file.razor__virtual.g.cs");
        var serverCapabilities = new VSInternalServerCapabilities()
        {
            HoverProvider = true
        };

        await using var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(
            csharpSourceText, csharpDocumentUri, serverCapabilities, razorSpanMappingService: null, capabilitiesUpdater: null, DisposalToken);
        await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString());

        var razorFilePath = "C:/path/to/file.razor";
        var documentContextFactory = new TestDocumentContextFactory(razorFilePath, codeDocument);
        var languageServerFeatureOptions = Mock.Of<LanguageServerFeatureOptions>(options =>
            options.SupportsFileManipulation == true &&
            options.SingleServerSupport == true &&
            options.CSharpVirtualDocumentSuffix == ".g.cs" &&
            options.HtmlVirtualDocumentSuffix == ".g.html" &&
            options.UseRazorCohostServer == false
            , MockBehavior.Strict);
        var languageServer = new HoverLanguageServer(csharpServer, csharpDocumentUri, DisposalToken);
        var documentMappingService = new LspDocumentMappingService(FilePathService, documentContextFactory, LoggerFactory);

        var service = GetHoverService(documentMappingService);

        var endpoint = new HoverEndpoint(
            service,
            languageServerFeatureOptions,
            documentMappingService,
            languageServer,
            LoggerFactory);

        var razorFileUri = new Uri(razorFilePath);
        var request = new TextDocumentPositionParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = razorFileUri,
            },
            Position = codeDocument.Source.Text.GetPosition(cursorPosition)
        };
        var documentContext = CreateDocumentContext(razorFileUri, codeDocument);
        var requestContext = CreateRazorRequestContext(documentContext: documentContext);

        return await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);
    }

    private DocumentContext CreateDefaultDocumentContext()
    {
        var txt = """
                @addTagHelper *, TestAssembly
                <any @test="Increment" />
                @code{
                    public void Increment(){
                    }
                }
                """;
        var path = "C:/text.razor";
        var codeDocument = CreateCodeDocument(txt, path, DefaultTagHelpers);
        var projectWorkspaceState = ProjectWorkspaceState.Create(DefaultTagHelpers);
        var projectSnapshot = TestProjectSnapshot.Create("C:/project.csproj", projectWorkspaceState);
        var sourceText = SourceText.From(txt);

        var snapshot = Mock.Of<IDocumentSnapshot>(d =>
            d.GetGeneratedOutputAsync(It.IsAny<bool>()) == Task.FromResult(codeDocument) &&
            d.FilePath == path &&
            d.FileKind == FileKinds.Component &&
            d.GetTextAsync() == Task.FromResult(sourceText) &&
            d.Version == 0 &&
            d.Project == projectSnapshot, MockBehavior.Strict);

        var documentContext = new DocumentContext(new Uri(path), snapshot, projectContext: null);

        return documentContext;
    }

    private HoverEndpoint CreateEndpoint(
        LanguageServerFeatureOptions languageServerFeatureOptions = null,
        IDocumentMappingService documentMappingService = null,
        IClientConnection clientConnection = null)
    {
        languageServerFeatureOptions ??= Mock.Of<LanguageServerFeatureOptions>(options => options.SupportsFileManipulation == true && options.SingleServerSupport == false, MockBehavior.Strict);

        var documentMappingServiceMock = new Mock<IDocumentMappingService>(MockBehavior.Strict);
        documentMappingServiceMock
            .Setup(c => c.GetLanguageKind(It.IsAny<RazorCodeDocument>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(RazorLanguageKind.Html);
        documentMappingService ??= documentMappingServiceMock.Object;

        clientConnection ??= Mock.Of<IClientConnection>(MockBehavior.Strict);

        var service = GetHoverService();

        var endpoint = new HoverEndpoint(
            service,
            languageServerFeatureOptions,
            documentMappingService,
            clientConnection,
            LoggerFactory);

        return endpoint;
    }

    private HoverService GetHoverService(IDocumentMappingService mappingService = null)
    {
        var projectManager = CreateProjectSnapshotManager();
        var lspTagHelperTooltipFactory = new DefaultLSPTagHelperTooltipFactory(projectManager);
        var vsLspTagHelperTooltipFactory = new DefaultVSLSPTagHelperTooltipFactory(projectManager);

        var clientCapabilities = CreateMarkDownCapabilities();
        clientCapabilities.SupportsVisualStudioExtensions = true;
        var clientCapabilitiesService = new TestClientCapabilitiesService(clientCapabilities);
        return new HoverService(lspTagHelperTooltipFactory, vsLspTagHelperTooltipFactory, mappingService, clientCapabilitiesService);
    }

    private class HoverLanguageServer : IClientConnection
    {
        private readonly CSharpTestLspServer _csharpServer;
        private readonly Uri _csharpDocumentUri;
        private readonly CancellationToken _cancellationToken;

        public HoverLanguageServer(
            CSharpTestLspServer csharpServer,
            Uri csharpDocumentUri,
            CancellationToken cancellationToken)
        {
            _csharpServer = csharpServer;
            _csharpDocumentUri = csharpDocumentUri;
            _cancellationToken = cancellationToken;
        }

        public Task SendNotificationAsync<TParams>(string method, TParams @params, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task SendNotificationAsync(string method, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<TResponse> SendRequestAsync<TParams, TResponse>(string method, TParams @params, CancellationToken cancellationToken)
        {
            Assert.Equal(CustomMessageNames.RazorHoverEndpointName, method);
            var hoverParams = Assert.IsType<DelegatedPositionParams>(@params);

            var hoverRequest = new TextDocumentPositionParams()
            {
                TextDocument = new VSTextDocumentIdentifier()
                {
                    Uri = _csharpDocumentUri,
                },
                Position = hoverParams.ProjectedPosition
            };

            var result = await _csharpServer.ExecuteRequestAsync<VisualStudio.LanguageServer.Protocol.TextDocumentPositionParams, TResponse>(
                Methods.TextDocumentHoverName, hoverRequest, _cancellationToken);

            return result;
        }
    }
}
