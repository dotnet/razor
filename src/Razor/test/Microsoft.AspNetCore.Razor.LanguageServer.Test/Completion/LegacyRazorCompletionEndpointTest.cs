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
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class LegacyRazorCompletionEndpointTest : LanguageServerTestBase
    {
        private readonly IReadOnlyList<CompletionItemKind> _supportedCompletionItemKinds = new[]
        {
            CompletionItemKind.Struct,
            CompletionItemKind.Keyword,
            CompletionItemKind.TagHelper,
        };

        public LegacyRazorCompletionEndpointTest()
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
            CompletionListCache = new CompletionListCache();
            ClientCapabilities = new VSInternalClientCapabilities()
            {
                TextDocument = new TextDocumentClientCapabilities()
                {
                    Completion = new VSInternalCompletionSetting()
                    {
                        CompletionItemKind = new CompletionItemKindSetting()
                        {
                            ValueSet = new[] { CompletionItemKind.TagHelper }
                        },
                        CompletionList = new VSInternalCompletionListSetting()
                        {
                            CommitCharacters = true,
                            Data = true,
                        }
                    }
                }
            };
        }

        private RazorCompletionFactsService CompletionFactsService { get; }

        private CompletionListCache CompletionListCache { get; }

        private VSInternalClientCapabilities ClientCapabilities { get; }

        [Fact]
        public void IsApplicableTriggerContext_Deletion_ReturnsFalse()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext()
            {
                InvokeKind = VSInternalCompletionInvokeKind.Deletion
            };

            // Act
            var result = LegacyRazorCompletionEndpoint.IsApplicableTriggerContext(completionContext);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsApplicableTriggerContext_Explicit_ReturnsTrue()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext()
            {
                InvokeKind = VSInternalCompletionInvokeKind.Explicit
            };

            // Act
            var result = LegacyRazorCompletionEndpoint.IsApplicableTriggerContext(completionContext);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsApplicableTriggerContext_Typing_ReturnsTrue()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext()
            {
                InvokeKind = VSInternalCompletionInvokeKind.Typing
            };

            // Act
            var result = LegacyRazorCompletionEndpoint.IsApplicableTriggerContext(completionContext);

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
            var result = LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

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
            LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

            // Act & Assert
            JsonConvert.SerializeObject(converted);
        }

        [Fact]
        public void TryConvert_DirectiveAttributeTransition_SerializationDoesNotThrow()
        {
            // Arrange
            var completionItem = DirectiveAttributeTransitionCompletionItemProvider.TransitionCompletionItem;
            LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

            // Act & Assert
            JsonConvert.SerializeObject(converted);
        }

        [Fact]
        public void TryConvert_DirectiveAttributeTransition_ReturnsTrue()
        {
            // Arrange
            var completionItem = DirectiveAttributeTransitionCompletionItemProvider.TransitionCompletionItem;

            // Act
            var result = LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

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
            var result = LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.DisplayText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Equal(converted.CommitCharacters, completionItem.CommitCharacters.Select(c => c.Character));
        }

        [Fact]
        public void TryConvert_MarkupTransition_SerializationDoesNotThrow()
        {
            // Arrange
            var completionItem = MarkupTransitionCompletionItemProvider.MarkupTransitionCompletionItem;
            LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

            // Act & Assert
            JsonConvert.SerializeObject(converted);
        }

        [Fact]
        public void TryConvert_DirectiveAttribute_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("@testDisplay", "testInsert", RazorCompletionItemKind.DirectiveAttribute, commitCharacters: RazorCommitCharacter.FromArray(new[] { "=", ":" }));

            // Act
            var result = LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal(completionItem.InsertText, converted.InsertText);
            Assert.Equal(completionItem.InsertText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Equal(completionItem.CommitCharacters.Select(c => c.Character), converted.CommitCharacters);
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
            var result = LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

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
            var result = LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

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
            var result = LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal("format", converted.InsertText);
            Assert.Equal(InsertTextFormat.Plaintext, converted.InsertTextFormat);
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
            var completionItem = new RazorCompletionItem("format", "format=\"$0\"", RazorCompletionItemKind.TagHelperAttribute, isSnippet: true);
            var attributeCompletionDescription = new AggregateBoundAttributeDescription(new BoundAttributeDescriptionInfo[] { });
            completionItem.SetAttributeCompletionDescription(attributeCompletionDescription);

            // Act
            var result = LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal("format=\"$0\"", converted.InsertText);
            Assert.Equal(InsertTextFormat.Snippet, converted.InsertTextFormat);
            Assert.Equal(completionItem.DisplayText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
        }

        [Fact]
        public void TryConvert_TagHelperAttribute_ReturnsTrue()
        {
            // Arrange
            var completionItem = new RazorCompletionItem("format", "format=\"$0\"", RazorCompletionItemKind.TagHelperAttribute, isSnippet: true);

            // Act
            var result = LegacyRazorCompletionEndpoint.TryConvert(completionItem, ClientCapabilities, out var converted);

            // Assert
            Assert.True(result);
            Assert.Equal(completionItem.DisplayText, converted.Label);
            Assert.Equal("format=\"$0\"", converted.InsertText);
            Assert.Equal(InsertTextFormat.Snippet, converted.InsertTextFormat);
            Assert.Equal(completionItem.DisplayText, converted.FilterText);
            Assert.Equal(completionItem.DisplayText, converted.SortText);
            Assert.Null(converted.Detail);
            Assert.Null(converted.Documentation);
            Assert.Null(converted.Command);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_Unsupported_NoCompletionItems()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument("@");
            codeDocument.SetUnsupported();
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);
            var completionEndpoint = new LegacyRazorCompletionEndpoint(
                documentContextFactory, CompletionFactsService, CompletionListCache, LoggerFactory);
            completionEndpoint.GetRegistration(ClientCapabilities);
            var request = new VSCompletionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = documentPath,
                },
                Position = new Position(0, 1),
                Context = new VSInternalCompletionContext(),
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Null(completionList);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ProvidesDirectiveCompletionItems()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument("@");
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);
            var completionEndpoint = new LegacyRazorCompletionEndpoint(
                documentContextFactory, CompletionFactsService, CompletionListCache, LoggerFactory);
            completionEndpoint.GetRegistration(ClientCapabilities);
            var request = new VSCompletionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = documentPath,
                },
                Position = new Position(0, 1),
                Context = new VSInternalCompletionContext(),
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert

            // These are the default directives that don't need to be separately registered, they should always be part of the completion list.
            Assert.Contains(completionList.Items, item => item.InsertText == "addTagHelper");
            Assert.Contains(completionList.Items, item => item.InsertText == "removeTagHelper");
            Assert.Contains(completionList.Items, item => item.InsertText == "tagHelperPrefix");
        }

        [Fact]
        public async Task Handle_ProvidesInjectOnIncomplete_KeywordIn()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.razor");
            var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestTagHelper", "TestAssembly");
            builder.TagMatchingRule(rule => rule.TagName = "Test");
            builder.SetTypeName("TestNamespace.TestTagHelper");
            var tagHelper = builder.Build();
            var tagHelperContext = TagHelperDocumentContext.Create(prefix: string.Empty, new[] { tagHelper });
            var codeDocument = CreateCodeDocument("@in");
            codeDocument.SetTagHelperContext(tagHelperContext);
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);
            var completionEndpoint = new LegacyRazorCompletionEndpoint(
                documentContextFactory, CompletionFactsService, CompletionListCache, LoggerFactory);
            completionEndpoint.GetRegistration(ClientCapabilities);
            var request = new VSCompletionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = documentPath,
                },
                Position = new Position(0, 1),
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerForIncompleteCompletions,
                },
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Contains(completionList.Items, item => item.InsertText == "addTagHelper");
        }

        [Fact]
        public async Task Handle_DoesNotProvideInjectOnInvoked()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.razor");
            var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestTagHelper", "TestAssembly");
            builder.TagMatchingRule(rule => rule.TagName = "Test");
            builder.SetTypeName("TestNamespace.TestTagHelper");
            var tagHelper = builder.Build();
            var tagHelperContext = TagHelperDocumentContext.Create(prefix: string.Empty, new[] { tagHelper });
            var codeDocument = CreateCodeDocument("@inje");
            codeDocument.SetTagHelperContext(tagHelperContext);
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);
            var completionEndpoint = new LegacyRazorCompletionEndpoint(
                documentContextFactory, CompletionFactsService, CompletionListCache, LoggerFactory);
            completionEndpoint.GetRegistration(ClientCapabilities);
            var request = new VSCompletionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = documentPath,
                },
                Position = new Position(0, 1),
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                },
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Empty(completionList.Items);
        }

        [Fact]
        public async Task Handle_ProvidesInjectOnIncomplete()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.razor");
            var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestTagHelper", "TestAssembly");
            builder.TagMatchingRule(rule => rule.TagName = "Test");
            builder.SetTypeName("TestNamespace.TestTagHelper");
            var tagHelper = builder.Build();
            var tagHelperContext = TagHelperDocumentContext.Create(prefix: string.Empty, new[] { tagHelper });
            var codeDocument = CreateCodeDocument("@inje");
            codeDocument.SetTagHelperContext(tagHelperContext);
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);
            var completionEndpoint = new LegacyRazorCompletionEndpoint(
                documentContextFactory, CompletionFactsService, CompletionListCache, LoggerFactory);
            completionEndpoint.GetRegistration(ClientCapabilities);
            var request = new VSCompletionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = documentPath,
                },
                Position = new Position(0, 1),
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerForIncompleteCompletions,
                },
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Contains(completionList.Items, item => item.InsertText == "addTagHelper");
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ProvidesTagHelperElementCompletionItems()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, "TestTagHelper", "TestAssembly");
            builder.TagMatchingRule(rule => rule.TagName = "Test");
            builder.SetTypeName("TestNamespace.TestTagHelper");
            var tagHelper = builder.Build();
            var tagHelperContext = TagHelperDocumentContext.Create(prefix: string.Empty, new[] { tagHelper });
            var codeDocument = CreateCodeDocument("<");
            codeDocument.SetTagHelperContext(tagHelperContext);
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);
            var completionEndpoint = new LegacyRazorCompletionEndpoint(
                documentContextFactory, CompletionFactsService, CompletionListCache, LoggerFactory);
            completionEndpoint.GetRegistration(ClientCapabilities);
            var request = new VSCompletionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = documentPath,
                },
                Position = new Position(0, 1),
                Context = new VSInternalCompletionContext(),
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Contains(completionList.Items, item => item.InsertText == "Test");
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ProvidesTagHelperAttributeItems()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
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
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);
            var completionEndpoint = new LegacyRazorCompletionEndpoint(
                documentContextFactory, CompletionFactsService, CompletionListCache, LoggerFactory);
            completionEndpoint.GetRegistration(ClientCapabilities);
            var request = new VSCompletionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = documentPath,
                },
                Position = new Position(0, 6),
                Context = new VSInternalCompletionContext(),
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Contains(completionList.Items, item => item.InsertText == "testAttribute=\"$0\"");
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
