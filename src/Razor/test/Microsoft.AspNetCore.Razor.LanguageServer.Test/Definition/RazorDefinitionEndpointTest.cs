// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Definition
{
    public class RazorDefinitionEndpointTest : TagHelperServiceTestBase
    {
        public RazorDefinitionEndpointTest(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_Element()
        {
            // Arrange
            var content = """
                @addTagHelper *, TestAssembly
                <te$$st1></test1>
                """;

            TestFileMarkupParser.GetPosition(content, out content, out var positioin);

            var srcText = SourceText.From(content);
            var codeDocument = CreateCodeDocument(content, isRazorFile: false, DefaultTagHelpers);
            var documentSnapshot = Mock.Of<DocumentSnapshot>(d => d.GetTextAsync() == Task.FromResult(srcText), MockBehavior.Strict);
            Mock.Get(documentSnapshot)
                .Setup(s => s.GetGeneratedOutputAsync())
                .ReturnsAsync(codeDocument);

            var documentContext = CreateDocumentContext(new Uri(@"C:\file.razor"), documentSnapshot);

            // Act
            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, positioin, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), DisposalToken);

            // Assert
            Assert.NotNull(descriptor);
            Assert.Equal("Test1TagHelper", descriptor!.Name);
            Assert.Null(attributeDescriptor);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_StartTag_WithAttribute()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Co$$mponent1 @test="Increment"></Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content, "Component1TagHelper");
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_EndTag_WithAttribute()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Component1 @test="Increment"></Comp$$onent1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content, "Component1TagHelper");
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_Attribute_ReturnsNull()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Component1 @te$$st="Increment"></Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_AttributeValue_ReturnsNull()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Component1 @test="Increm$$ent"></Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_AfterAttributeEquals_ReturnsNull()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Component1 @test="$$Increment"></Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_AttributeEnd_ReturnsNull()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Component1 @test="Increment">$$</Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_MultipleAttributes()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Co$$mponent1 @test="Increment" @minimized></Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content, "Component1TagHelper");
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_MalformedElement()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Co$$mponent1</Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content, "Component1TagHelper");
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_MalformedAttribute()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Co$$mponent1 @test="Increment></Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content, "Component1TagHelper");
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_HTML_MarkupElement()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <p>
                    <str$$ong></strong>
                </p>
                """;

            await VerifyOriginTagHelperBindingAsync(content);
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_PropertyAttribute()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Component1 boo$$l-val="true"></Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content, "Component1TagHelper", "BoolVal");
        }

        [Fact]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_MinimizedPropertyAttribute()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Component1 boo$$l-val></Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content, "Component1TagHelper", "BoolVal");
        }

        [Fact, WorkItem("https://github.com/dotnet/razor-tooling/issues/6775")]
        public async Task GetOriginTagHelperBindingAsync_TagHelper_PropertyAttributeEdge()
        {
            var content = """
                @addTagHelper *, TestAssembly
                <Component1 bool-val$$="true"></Component1>
                @code {
                    public void Increment()
                    {
                    }
                }
                """;

            await VerifyOriginTagHelperBindingAsync(content, "Component1TagHelper", "BoolVal");
        }

        [Fact]
        public async Task GetNavigatePositionAsync_TagHelperProperty_CorrectRange1()
        {
            var content = """
                <div>@Title</div>

                @code
                {
                    [Parameter]
                    public string NotTitle { get; set; }

                    [Parameter]
                    public string [|Title|] { get; set; }
                }
                """;

            await VerifyNavigatePositionAsync(content, "Title");
        }

        [Fact]
        public async Task GetNavigatePositionAsync_TagHelperProperty_CorrectRange2()
        {
            var content = """
                <div>@Title</div>

                @code
                {
                    [Microsoft.AspNetCore.Components.Parameter]
                    public string [|Title|] { get; set; }
                }
                """;

            await VerifyNavigatePositionAsync(content, "Title");
        }

        [Fact]
        public async Task GetNavigatePositionAsync_TagHelperProperty_CorrectRange3()
        {
            var content = """
                <div>@Title</div>

                @code
                {
                    [Components.ParameterAttribute]
                    public string [|Title|] { get; set; }
                }
                """;

            await VerifyNavigatePositionAsync(content, "Title");
        }

        [Fact]
        public async Task GetNavigatePositionAsync_TagHelperProperty_IgnoreInnerProperty()
        {
            var content = """
                <div>@Title</div>

                @code
                {
                    private class NotTheDroidsYoureLookingFor
                    {
                        public string Title { get; set; }
                    }

                    public string [|Title|] { get; set; }
                }
                """;

            await VerifyNavigatePositionAsync(content, "Title");
        }

        #region Helpers
        private async Task VerifyOriginTagHelperBindingAsync(string content, string tagHelperDescriptorName = null, string attributeDescriptorPropertyName = null)
        {
            TestFileMarkupParser.GetPosition(content, out content, out var position);

            SetupDocument(out var _, out var documentSnapshot, content);
            var documentContext = CreateDocumentContext(new Uri(@"C:\file.razor"), documentSnapshot);

            var (descriptor, attributeDescriptor) = await RazorDefinitionEndpoint.GetOriginTagHelperBindingAsync(
                documentContext, position, LoggerFactory.CreateLogger("RazorDefinitionEndpoint"), DisposalToken);

            if (tagHelperDescriptorName is null)
            {
                Assert.Null(descriptor);
            }
            else
            {
                Assert.NotNull(descriptor);
                Assert.Equal(tagHelperDescriptorName, descriptor!.Name);
            }

            if (attributeDescriptorPropertyName is null)
            {
                Assert.Null(attributeDescriptor);
            }
            else
            {
                Assert.NotNull(attributeDescriptor);
                Assert.Equal(attributeDescriptorPropertyName, attributeDescriptor.GetPropertyName());
            }
        }

        private async Task VerifyNavigatePositionAsync(string content, string propertyName)
        {
            TestFileMarkupParser.GetSpan(content, out content, out var selection);

            SetupDocument(out var codeDocument, out _, content);
            var expectedRange = selection.AsRange(codeDocument.GetSourceText());

            var mappingService = new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);

            var range = await RazorDefinitionEndpoint.TryGetPropertyRangeAsync(codeDocument, propertyName, mappingService, Logger, DisposalToken);
            Assert.NotNull(range);
            Assert.Equal(expectedRange, range);
        }

        private void SetupDocument(out RazorCodeDocument codeDocument, out DocumentSnapshot documentSnapshot, string content)
        {
            var sourceText = SourceText.From(content);
            codeDocument = CreateCodeDocument(content, "text.razor", DefaultTagHelpers);
            var outDoc = codeDocument;
            documentSnapshot = Mock.Of<DocumentSnapshot>(
                d => d.GetTextAsync() == Task.FromResult(sourceText),
                MockBehavior.Strict);
            Mock.Get(documentSnapshot)
                .Setup(s => s.GetGeneratedOutputAsync())
                .ReturnsAsync(outDoc);
        }
        #endregion
    }
}
