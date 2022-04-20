// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Editor.Razor;
using Moq;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class RazorCompletionEndpointTest : LanguageServerTestBase
    {
        private readonly IReadOnlyList<ExtendedCompletionItemKinds> _supportedCompletionItemKinds = new[]
        {
            ExtendedCompletionItemKinds.Struct,
            ExtendedCompletionItemKinds.Keyword,
            ExtendedCompletionItemKinds.TagHelper,
        };

        public RazorCompletionEndpointTest()
        {
            // Working around strong naming restriction.
            var tagHelperFactsService = new DefaultTagHelperFactsService();
            var tagHelperCompletionService = new LanguageServerTagHelperCompletionService(tagHelperFactsService);
            var completionProviders = new RazorCompletionItemProvider[]
            {
                new DirectiveCompletionItemProvider(),
                new DirectiveAttributeCompletionItemProvider(tagHelperFactsService),
                new DirectiveAttributeParameterCompletionItemProvider(tagHelperFactsService),
                new TagHelperCompletionProvider(tagHelperCompletionService, new DefaultHtmlFactsService(), tagHelperFactsService)
            };
            CompletionFactsService = new DefaultRazorCompletionFactsService(completionProviders);
            EmptyDocumentResolver = Mock.Of<DocumentResolver>(MockBehavior.Strict);
            CompletionListCache = new CompletionListCache();
        }

        private RazorCompletionFactsService CompletionFactsService { get; }

        private DocumentResolver EmptyDocumentResolver { get; }

        private CompletionListCache CompletionListCache { get; }

        [Fact]
        public void IsApplicableTriggerContext_Deletion_ReturnsFalse()
        {
            // Arrange
            var completionContext = new OmniSharpVSCompletionContext()
            {
                InvokeKind = OmniSharpVSCompletionInvokeKind.Deletion
            };

            // Act
            var result = RazorCompletionEndpoint.IsApplicableTriggerContext(completionContext);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsApplicableTriggerContext_Explicit_ReturnsTrue()
        {
            // Arrange
            var completionContext = new OmniSharpVSCompletionContext()
            {
                InvokeKind = OmniSharpVSCompletionInvokeKind.Explicit
            };

            // Act
            var result = RazorCompletionEndpoint.IsApplicableTriggerContext(completionContext);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsApplicableTriggerContext_Typing_ReturnsTrue()
        {
            // Arrange
            var completionContext = new OmniSharpVSCompletionContext()
            {
                InvokeKind = OmniSharpVSCompletionInvokeKind.Typing
            };

            // Act
            var result = RazorCompletionEndpoint.IsApplicableTriggerContext(completionContext);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void TryConvert_Directive_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("testDisplay", "testInsert", RazorCompletionItemKind.Directive);
            var description = "Something";
            completionItem.SetDirectiveCompletionDescription(new DirectiveCompletionDescription(description));

            // Act
            var result = RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.DisplayText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
        }

        [Fact]
        public void TryConvert_Directive_SerializationDoesNotThrow()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("testDisplay", "testInsert", RazorCompletionItemKind.Directive);
            var description = "Something";
            completionItem.SetDirectiveCompletionDescription(new DirectiveCompletionDescription(description));
            RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Act & Assert
            JsonConvert.SerializeObject(converted);
        }

        [Fact]
        public void TryConvert_DirectiveAttributeTransition_SerializationDoesNotThrow()
        {
            // Arrange
            var completionItem = DirectiveAttributeTransitionCompletionItemProvider.TransitionCompletionItem;
            RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Act & Assert
            JsonConvert.SerializeObject(converted);
        }

        [Fact]
        public void TryConvert_DirectiveAttributeTransition_ReturnsTrue()
        {
            // Arrange
            var completionItem = DirectiveAttributeTransitionCompletionItemProvider.TransitionCompletionItem;

            // Act
            var result = RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Assert
            Assert.True(result);
            Assert.False(converted.Preselect);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.DisplayText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.NotNull(converted.Command);
        }

        [Fact]
        public void TryConvert_MarkupTransition_ReturnsTrue()
        {
            // Arrange
            var completionItem = MarkupTransitionCompletionItemProvider.MarkupTransitionCompletionItem;

            // Act
            var result = RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.DisplayText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Equal(converted.CommitCharacters, completionItem.CommitCharacters);
        }

        [Fact]
        public void TryConvert_MarkupTransition_SerializationDoesNotThrow()
        {
            // Arrange
            var completionItem = MarkupTransitionCompletionItemProvider.MarkupTransitionCompletionItem;
            RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Act & Assert
            JsonConvert.SerializeObject(converted);
        }

        [Fact]
        public void TryConvert_DirectiveAttribute_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("@testDisplay", "testInsert", RazorCompletionItemKind.DirectiveAttribute, commitCharacters: new[] { "=", ":" });

            // Act
            var result = RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Equal(completionItem.CommitCharacters, converted.CommitCharacters);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
        }

        [Fact]
        public void TryConvert_DirectiveAttributeParameter_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("format", "format", RazorCompletionItemKind.DirectiveAttributeParameter);

            // Act
            var result = RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.InsertText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
        }

        [Fact]
        public void TryConvert_TagHelperElement_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("format", "format", RazorCompletionItemKind.TagHelperElement);

            // Act
            var result = RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.InsertText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
        }

        [Fact]
        public void TryConvert_TagHelperAttribute_ForBool_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("format", "format", RazorCompletionItemKind.TagHelperAttribute);
            var attributeCompletionDescription = new AggregateBoundAttributeDescription(new[] {
                new BoundAttributeDescriptionInfo("System.Boolean", "Stuff", "format", "SomeDocs")
            });
            completionItem.SetAttributeCompletionDescription(attributeCompletionDescription);

            // Act
            var result = RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal("format", converted.InsertText);
            Assert.Equal(InsertTextFormat.PlainText, converted.InsertTextFormat);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.InsertText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
        }

        [Fact]
        public void TryConvert_TagHelperAttribute_ForHtml_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("format", "format", RazorCompletionItemKind.TagHelperAttribute);
            var attributeCompletionDescription = new AggregateBoundAttributeDescription(new BoundAttributeDescriptionInfo[] { });
            completionItem.SetAttributeCompletionDescription(attributeCompletionDescription);

            // Act
            var result = RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal("format=\"$0\"", converted.InsertText);
            Assert.Equal(InsertTextFormat.Snippet, converted.InsertTextFormat);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.InsertText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
        }

        [Fact]
        public void TryConvert_TagHelperAttribute_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("format", "format", RazorCompletionItemKind.TagHelperAttribute);

            // Act
            var result = RazorCompletionEndpoint.TryConvert(completionItem, _supportedCompletionItemKinds, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal("format=\"$0\"", converted.InsertText);
            Assert.Equal(InsertTextFormat.Snippet, converted.InsertTextFormat);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.InsertText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_Unsupported_NoCompletionItems()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument("@");
            codeDocument.SetUnsupported();
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(
                Dispatcher, documentResolver, CompletionFactsService, CompletionListCache, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 1),
                Context = new OmniSharpVSCompletionContext(),
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Empty(completionList);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ProvidesDirectiveCompletionItems()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument("@");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(
                Dispatcher, documentResolver, CompletionFactsService, CompletionListCache, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 1),
                Context = new OmniSharpVSCompletionContext(),
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert

            // These are the default directives that don't need to be separately registered, they should always be part of the completion list.
            Assert.Contains(completionList, item => item.InsertText == "addTagHelper");
            Assert.Contains(completionList, item => item.InsertText == "removeTagHelper");
            Assert.Contains(completionList, item => item.InsertText == "tagHelperPrefix");
        }

        [Fact]
        public async Task Handle_ProvidesInjectOnIncomplete_KeywordIn()
        {
            // Arrange
            var documentPath = "C:/path/to/document.razor";
            var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestTagHelper", "TestAssembly");
            builder.TagMatchingRule(rule => rule.TagName = "Test");
            builder.SetTypeName("TestNamespace.TestTagHelper");
            var tagHelper = builder.Build();
            var tagHelperContext = TagHelperDocumentContext.Create(prefix: string.Empty, new[] { tagHelper });
            var codeDocument = CreateCodeDocument("@in");
            codeDocument.SetTagHelperContext(tagHelperContext);
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(
                Dispatcher, documentResolver, CompletionFactsService, CompletionListCache, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 1),
                Context = new OmniSharpVSCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerForIncompleteCompletions,
                },
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Contains(completionList, item => item.InsertText == "addTagHelper");
        }

        [Fact]
        public async Task Handle_DoesNotProvideInjectOnInvoked()
        {
            // Arrange
            var documentPath = "C:/path/to/document.razor";
            var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestTagHelper", "TestAssembly");
            builder.TagMatchingRule(rule => rule.TagName = "Test");
            builder.SetTypeName("TestNamespace.TestTagHelper");
            var tagHelper = builder.Build();
            var tagHelperContext = TagHelperDocumentContext.Create(prefix: string.Empty, new[] { tagHelper });
            var codeDocument = CreateCodeDocument("@inje");
            codeDocument.SetTagHelperContext(tagHelperContext);
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(
                Dispatcher, documentResolver, CompletionFactsService, CompletionListCache, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 1),
                Context = new OmniSharpVSCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                },
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Empty(completionList);
        }

        [Fact]
        public async Task Handle_ProvidesInjectOnIncomplete()
        {
            // Arrange
            var documentPath = "C:/path/to/document.razor";
            var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestTagHelper", "TestAssembly");
            builder.TagMatchingRule(rule => rule.TagName = "Test");
            builder.SetTypeName("TestNamespace.TestTagHelper");
            var tagHelper = builder.Build();
            var tagHelperContext = TagHelperDocumentContext.Create(prefix: string.Empty, new[] { tagHelper });
            var codeDocument = CreateCodeDocument("@inje");
            codeDocument.SetTagHelperContext(tagHelperContext);
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(
                Dispatcher, documentResolver, CompletionFactsService, CompletionListCache, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 1),
                Context = new OmniSharpVSCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerForIncompleteCompletions,
                },
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Contains(completionList, item => item.InsertText == "addTagHelper");
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ProvidesTagHelperElementCompletionItems()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestTagHelper", "TestAssembly");
            builder.TagMatchingRule(rule => rule.TagName = "Test");
            builder.SetTypeName("TestNamespace.TestTagHelper");
            var tagHelper = builder.Build();
            var tagHelperContext = TagHelperDocumentContext.Create(prefix: string.Empty, new[] { tagHelper });
            var codeDocument = CreateCodeDocument("<");
            codeDocument.SetTagHelperContext(tagHelperContext);
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(
                Dispatcher, documentResolver, CompletionFactsService, CompletionListCache, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 1),
                Context = new OmniSharpVSCompletionContext(),
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Contains(completionList, item => item.InsertText == "Test");
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ProvidesTagHelperAttributeItems()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestTagHelper", "TestAssembly");
            builder.TagMatchingRule(rule => rule.TagName = "*");
            builder.BindAttribute(attribute =>
            {
                attribute.Name = "testAttribute";
                attribute.TypeName = typeof(string).FullName;
                attribute.SetPropertyName("TestAttribute");
            });
            builder.SetTypeName("TestNamespace.TestTagHelper");
            var tagHelper = builder.Build();
            var tagHelperContext = TagHelperDocumentContext.Create(prefix: string.Empty, new[] { tagHelper });
            var codeDocument = CreateCodeDocument("<test  ");
            codeDocument.SetTagHelperContext(tagHelperContext);
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var completionEndpoint = new RazorCompletionEndpoint(
                Dispatcher, documentResolver, CompletionFactsService, CompletionListCache, LoggerFactory);
            var request = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Position = new Position(0, 6),
                Context = new OmniSharpVSCompletionContext(),
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Contains(completionList, item => item.InsertText == "testAttribute=\"$0\"");
        }

        private static DocumentResolver CreateDocumentResolver(string documentPath, RazorCodeDocument codeDocument)
        {
            var sourceTextChars = new char[codeDocument.Source.Length];
            codeDocument.Source.CopyTo(0, sourceTextChars, 0, codeDocument.Source.Length);
            var sourceText = SourceText.From(new string(sourceTextChars));
            var documentSnapshot = Mock.Of<DocumentSnapshot>(document =>
                document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                document.GetTextAsync() == Task.FromResult(sourceText), MockBehavior.Strict);
            var documentResolver = new Mock<DocumentResolver>(MockBehavior.Strict);
            documentResolver.Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);
            return documentResolver.Object;
        }

        private static RazorCodeDocument CreateCodeDocument(string text)
        {
            var codeDocument = TestRazorCodeDocument.CreateEmpty();
            var sourceDocument = TestRazorSourceDocument.Create(text);
            var syntaxTree = RazorSyntaxTree.Parse(sourceDocument);
            codeDocument.SetSyntaxTree(syntaxTree);
            var tagHelperDocumentContext = TagHelperDocumentContext.Create(prefix: string.Empty, Enumerable.Empty<TagHelperDescriptor>());
            codeDocument.SetTagHelperContext(tagHelperDocumentContext);
            return codeDocument;
        }
    }
}
