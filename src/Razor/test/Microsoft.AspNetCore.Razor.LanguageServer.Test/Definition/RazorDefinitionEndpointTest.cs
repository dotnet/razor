// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Definition
{
    public class RazorDefinitionEndpointTest : TagHelperServiceTestBase
    {
        private const string DefaultContent = @"@addTagHelper *, TestAssembly
<Component1 @test=""Increment""></Component1>
@code {
    public void Increment()
    {
    }
}";
        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_Element()
        {
            // Arrange
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1></test1>";
            var srcText = SourceText.From(txt);
            var codeDocument = CreateCodeDocument(txt, isRazorFile: false, DefaultTagHelpers);
            var documentSnapshot = Mock.Of<DocumentSnapshot>(d => d.GetTextAsync() == Task.FromResult(srcText), MockBehavior.Strict);
            Mock.Get(documentSnapshot).Setup(s => s.GetGeneratedOutputAsync()).Returns(Task.FromResult(codeDocument));

            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 34, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal("Test1TagHelper", descriptor!.Name);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_StartTag_WithAttribute()
        {
            // Arrange
            SetupDocument(out var _, out var documentSnapshot);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 34, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal("Component1TagHelper", descriptor!.Name);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_EndTag_WithAttribute()
        {
            // Arrange
            SetupDocument(out var _, out var documentSnapshot);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 67, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal("Component1TagHelper", descriptor!.Name);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_Attribute_ReturnsNull()
        {
            // Arrange
            SetupDocument(out var _, out var documentSnapshot);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (binding, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 46, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(binding);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_AttributeValue_ReturnsNull()
        {
            // Arrange
            SetupDocument(out var _, out var documentSnapshot);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (binding, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 56, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(binding);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_AfterAttributeEquals_ReturnsNull()
        {
            // Arrange
            SetupDocument(out var _, out var documentSnapshot);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (binding, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 50, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(binding);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_AttributeEnd_ReturnsNull()
        {
            // Arrange
            SetupDocument(out var _, out var documentSnapshot);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (binding, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 61, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(binding);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_MultipleAttributes()
        {
            // Arrange
            var content = @"@addTagHelper *, TestAssembly
<Component1 @test=""Increment"" @minimized></Component1>
@code {
    public void Increment()
    {
    }
}";
            SetupDocument(out var _, out var documentSnapshot, content);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 34, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal("Component1TagHelper", descriptor!.Name);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_MalformedElement()
        {
            // Arrange
            var content = @"@addTagHelper *, TestAssembly
<Component1</Component1>
@code {
    public void Increment()
    {
    }
}";
            SetupDocument(out var _, out var documentSnapshot, content);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 34, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal("Component1TagHelper", descriptor!.Name);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_MalformedAttribute()
        {

            // Arrange
            var content = @"@addTagHelper *, TestAssembly
<Component1 @test=""Increment></Component1>
@code {
    public void Increment()
    {
    }
}";
            SetupDocument(out var _, out var documentSnapshot, content);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 34, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal("Component1TagHelper", descriptor!.Name);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_HTML_MarkupElement()
        {
            // Arrange
            var content = $"@addTagHelper *, TestAssembly{Environment.NewLine}<p><strong></strong></p>";
            SetupDocument(out var _, out var documentSnapshot, content);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (binding, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 38, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(binding);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_PropertyAttribute()
        {

            // Arrange
            var content = @"@addTagHelper *, TestAssembly
<Component1 bool-val=""true""></Component1>
@code {
    public void Increment()
    {
    }
}";
            SetupDocument(out var _, out var documentSnapshot, content);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 46, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal("Component1TagHelper", descriptor!.Name);
            Assert.NotNull(attributeDescriptor);
            Assert.Equal("BoolVal", attributeDescriptor.GetPropertyName());
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_MinimizedPropertyAttribute()
        {

            // Arrange
            var content = @"@addTagHelper *, TestAssembly
<Component1 bool-val></Component1>
@code {
    public void Increment()
    {
    }
}";
            SetupDocument(out var _, out var documentSnapshot, content);
            var documentContext = CreateDocumentContext(new Uri("C:\\file.razor"), documentSnapshot);

            // Act
            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, 46, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal("Component1TagHelper", descriptor!.Name);
            Assert.NotNull(attributeDescriptor);
            Assert.Equal("BoolVal", attributeDescriptor.GetPropertyName());
        }

        [Fact]
        public async Task GetNavigatePositionAsync_TagHelperProperty_CorrectRange1()
        {
            // Arrange
            var content = @"

<div>@Title</div>

@code
{
    [Parameter]
    public string NotTitle { get; set; }

    [Parameter]
    public string [|Title|] { get; set; }
}
";
            TestFileMarkupParser.GetSpan(content, out content, out var selection);

            SetupDocument(out var codeDocument, out _, content);
            var expectedRange = selection.AsRange(codeDocument.GetSourceText());

            var mappingService = new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);

            // Act II
            var range = await RazorDefinitionEndpoint.TryGetPropertyRangeAsync(codeDocument, "Title", mappingService, Logger, CancellationToken.None).ConfigureAwait(false);
            Assert.NotNull(range);
            Assert.Equal(expectedRange, range);
        }

        [Fact]
        public async Task GetNavigatePositionAsync_TagHelperProperty_CorrectRange2()
        {
            // Arrange
            var content = @"

<div>@Title</div>

@code
{
    [Microsoft.AspNetCore.Components.Parameter]
    public string [|Title|] { get; set; }
}
";
            TestFileMarkupParser.GetSpan(content, out content, out var selection);

            SetupDocument(out var codeDocument, out _, content);
            var expectedRange = selection.AsRange(codeDocument.GetSourceText());

            var mappingService = new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);

            // Act II
            var range = await RazorDefinitionEndpoint.TryGetPropertyRangeAsync(codeDocument, "Title", mappingService, Logger, CancellationToken.None).ConfigureAwait(false);
            Assert.NotNull(range);
            Assert.Equal(expectedRange, range);
        }

        [Fact]
        public async Task GetNavigatePositionAsync_TagHelperProperty_CorrectRange3()
        {
            // Arrange
            var content = @"

<div>@Title</div>

@code
{
    [Components.ParameterAttribute]
    public string [|Title|] { get; set; }
}
";
            TestFileMarkupParser.GetSpan(content, out content, out var selection);

            SetupDocument(out var codeDocument, out _, content);
            var expectedRange = selection.AsRange(codeDocument.GetSourceText());

            var mappingService = new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);

            // Act II
            var range = await RazorDefinitionEndpoint.TryGetPropertyRangeAsync(codeDocument, "Title", mappingService, Logger, CancellationToken.None).ConfigureAwait(false);
            Assert.NotNull(range);
            Assert.Equal(expectedRange, range);
        }

        [Fact]
        public async Task GetNavigatePositionAsync_TagHelperProperty_IgnoreInnerProperty()
        {
            // Arrange
            var content = @"

<div>@Title</div>

@code
{
    private class NotTheDroidsYoureLookingFor
    {
        public string Title { get; set; }
    }

    public string [|Title|] { get; set; }
}
";
            TestFileMarkupParser.GetSpan(content, out content, out var selection);

            SetupDocument(out var codeDocument, out _, content);
            var expectedRange = selection.AsRange(codeDocument.GetSourceText());

            var mappingService = new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);

            // Act II
            var range = await RazorDefinitionEndpoint.TryGetPropertyRangeAsync(codeDocument, "Title", mappingService, Logger, CancellationToken.None).ConfigureAwait(false);
            Assert.NotNull(range);
            Assert.Equal(expectedRange, range);
        }

        private void SetupDocument(out Language.RazorCodeDocument codeDocument, out DocumentSnapshot documentSnapshot, string content = DefaultContent)
        {
            var sourceText = SourceText.From(content);
            codeDocument = CreateCodeDocument(content, "text.razor", DefaultTagHelpers);
            var outDoc = codeDocument;
            documentSnapshot = Mock.Of<DocumentSnapshot>(d => d.GetTextAsync() == Task.FromResult(sourceText), MockBehavior.Strict);
            Mock.Get(documentSnapshot).Setup(s => s.GetGeneratedOutputAsync()).Returns(Task.FromResult(outDoc));
        }
    }
}
