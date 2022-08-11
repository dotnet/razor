// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Xunit;
using DefinitionResult = Microsoft.VisualStudio.LanguageServer.Protocol.SumType<
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalLocation,
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalLocation[],
    Microsoft.VisualStudio.LanguageServer.Protocol.DocumentLink[]>;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Definition
{
    [UseExportProvider]
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

        [Fact]
        public async Task Handle_SingleServer_CSharp_Method()
        {
            var input = """
                <div></div>

                @{
                    var x = Ge$$tX();
                }

                @functions
                {
                    void [|GetX|]()
                    {
                    }
                }
                """;

            await VerifyCSharpGoToDefinitionAsync(input);
        }

        [Fact]
        public async Task Handle_SingleServer_CSharp_Local()
        {
            var input = """
                <div></div>

                @{
                    var x = GetX();
                }

                @functions
                {
                    private string [|_name|];

                    string GetX()
                    {
                        return _na$$me;
                    }
                }
                """;

            await VerifyCSharpGoToDefinitionAsync(input);
        }

        [Fact]
        public async Task Handle_SingleServer_CSharp_MetadataReference()
        {
            var input = """
                <div></div>

                @functions
                {
                    private stri$$ng _name;
                }
                """;

            // Arrange
            TestFileMarkupParser.GetPosition(input, out var output, out var cursorPosition);

            var codeDocument = CreateCodeDocument(output);
            var csharpDocumentUri = new Uri("C:/path/to/file.razor__virtual.g.cs");

            // Act
            var result = await GetDefinitionResultAsync(codeDocument, csharpDocumentUri, cursorPosition);

            // Assert
            Assert.NotNull(result.Value.Second);
            var locations = result.Value.Second;
            var location = Assert.Single(locations);
            Assert.EndsWith("String.cs", location.Uri.ToString());
            Assert.Equal(21, location.Range.Start.Line);
        }

        private async Task VerifyCSharpGoToDefinitionAsync(string input)
        {
            // Arrange
            TestFileMarkupParser.GetPositionAndSpan(input, out var output, out var cursorPosition, out var expectedSpan);

            var codeDocument = CreateCodeDocument(output);
            var csharpDocumentUri = new Uri("C:/path/to/file.razor__virtual.g.cs");

            // Act
            var result = await GetDefinitionResultAsync(codeDocument, csharpDocumentUri, cursorPosition);

            // Assert
            Assert.NotNull(result.Value.Second);
            var locations = result.Value.Second;
            var location = Assert.Single(locations);
            Assert.Equal(csharpDocumentUri, location.Uri);

            var expectedRange = expectedSpan.AsRange(codeDocument.GetSourceText());
            Assert.Equal(expectedRange, location.Range);
        }

        private async Task<DefinitionResult?> GetDefinitionResultAsync(RazorCodeDocument codeDocument, Uri csharpDocumentUri, int cursorPosition)
        {
            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var serverCapabilities = new ServerCapabilities()
            {
                DefinitionProvider = true
            };
            var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(csharpSourceText, csharpDocumentUri, serverCapabilities, razorSpanMappingService: null).ConfigureAwait(false);
            await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString()).ConfigureAwait(false);

            var razorFilePath = "C:/path/to/file.razor";
            var documentContextFactory = new TestDocumentContextFactory(razorFilePath, codeDocument, version: 1337);
            var languageServerFeatureOptions = Mock.Of<LanguageServerFeatureOptions>(options =>
                options.SupportsFileManipulation == true &&
                options.SingleServerSupport == true &&
                options.CSharpVirtualDocumentSuffix == ".g.cs" &&
                options.HtmlVirtualDocumentSuffix == ".g.html"
                , MockBehavior.Strict);
            var languageServer = new DefinitionLanguageServer(csharpServer, csharpDocumentUri);
            var documentMappingService = new DefaultRazorDocumentMappingService(languageServerFeatureOptions, documentContextFactory, LoggerFactory);
            var projectSnapshotManager = Mock.Of<ProjectSnapshotManagerBase>(p => p.Projects == new[] { Mock.Of<ProjectSnapshot>(MockBehavior.Strict) }, MockBehavior.Strict);
            var projectSnapshotManagerAccessor = new TestProjectSnapshotManagerAccessor(projectSnapshotManager);
            var projectSnapshotManagerDispatcher = new LSPProjectSnapshotManagerDispatcher(LoggerFactory);
            var searchEngine = new DefaultRazorComponentSearchEngine(Dispatcher, projectSnapshotManagerAccessor, LoggerFactory);

            var endpoint = new RazorDefinitionEndpoint(documentContextFactory, searchEngine, documentMappingService, languageServerFeatureOptions, languageServer, TestLoggerFactory.Instance);

            codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);
            var request = new DefinitionParamsBridge
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = new Uri(razorFilePath)
                },
                Position = new Position(line, offset)
            };

            return await endpoint.Handle(request, CancellationToken.None);
        }

        private class DefinitionLanguageServer : ClientNotifierServiceBase
        {
            private readonly CSharpTestLspServer _csharpServer;
            private readonly Uri _csharpDocumentUri;

            public DefinitionLanguageServer(CSharpTestLspServer csharpServer, Uri csharpDocumentUri)
            {
                _csharpServer = csharpServer;
                _csharpDocumentUri = csharpDocumentUri;
            }

            public override OmniSharp.Extensions.LanguageServer.Protocol.Models.InitializeParams ClientSettings { get; }

            public override Task OnStarted(ILanguageServer server, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public override Task<IResponseRouterReturns> SendRequestAsync(string method)
            {
                throw new NotImplementedException();
            }

            public async override Task<IResponseRouterReturns> SendRequestAsync<T>(string method, T @params)
            {
                Assert.Equal(RazorLanguageServerCustomMessageTargets.RazorDefinitionEndpointName, method);
                var definitionParams = Assert.IsType<DelegatedPositionParams>(@params);

                var definitionRequest = new TextDocumentPositionParams()
                {
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = _csharpDocumentUri
                    },
                    Position = definitionParams.ProjectedPosition
                };

                var result = await _csharpServer.ExecuteRequestAsync<TextDocumentPositionParams, DefinitionResult?>(Methods.TextDocumentDefinitionName, definitionRequest, CancellationToken.None);

                return new TestResponseRouterReturn(result);
            }
        }
    }
}
