// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.VisualStudio.Editor.Razor;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class TagHelperCompletionProviderTest : TagHelperServiceTestBase
    {
        protected static ILanguageServer LanguageServer
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
                                        SnippetSupport = true
                                    }
                                }
                            }
                        }
                    }
                };

                var languageServer = new Mock<ILanguageServer>(MockBehavior.Strict);
                languageServer.SetupGet(server => server.ClientSettings)
                    .Returns(initializeParams);

                return languageServer.Object;
            }
        }

        [Fact]
        public void GetNearestAncestorTagInfo_MarkupElement()
        {
            // Arrange
            var context = CreateRazorCompletionContext(absoluteIndex: 33 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<p><strong></strong></p>", isRazorFile: false);
            var element = context.Owner.FirstAncestorOrSelf<MarkupElementSyntax>();
            var service = new DefaultTagHelperFactsService();

            // Act
            var (ancestorName, ancestorIsTagHelper) = service.GetNearestAncestorTagInfo(element.Ancestors());

            // Assert
            Assert.Equal("p", ancestorName);
            Assert.False(ancestorIsTagHelper);
        }

        [Fact]
        public void GetNearestAncestorTagInfo_TagHelperElement()
        {
            // Arrange
            var context = CreateRazorCompletionContext(absoluteIndex: 37 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1><test2></test2></test1>", isRazorFile: false, tagHelpers: DefaultTagHelpers);
            var element = context.Owner.FirstAncestorOrSelf<MarkupTagHelperElementSyntax>();
            var service = new DefaultTagHelperFactsService();

            // Act
            var (ancestorName, ancestorIsTagHelper) = service.GetNearestAncestorTagInfo(element.Ancestors());

            // Assert
            Assert.Equal("test1", ancestorName);
            Assert.True(ancestorIsTagHelper);
        }

        [Fact]
        public void GetCompletionAt_AtEmptyTagName_ReturnsCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 30 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion => Assert.Equal("test1", completion.InsertText),
                completion => Assert.Equal("test2", completion.InsertText));
        }

        [Fact]
        public void GetCompletionAt_OutsideOfTagName_DoesNotReturnCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 33 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<br />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionAt_MalformedElement()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 32 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}</t", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion =>
                {
                    Assert.Equal("test1", completion.InsertText);
                },
                completion =>
                {
                    Assert.Equal("test2", completion.InsertText);
                });
        }

        [Fact]
        public void GetCompletionAt_AtHtmlElementNameEdge_ReturnsNoCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 32 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<br />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionAt_AtTagHelperElementNameEdge_ReturnsNoCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 35 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionAt_AtAttributeEdge_IntAttribute_ReturnsCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 36 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion =>
                {
                    Assert.Equal("bool-val", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.MinimizedAttributeCommitCharacters, completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                },
                completion =>
                {
                    Assert.Equal("int-val", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.AttributeCommitCharacters, completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                });
        }

        [Fact]
        public void GetCompletionAt_AtAttributeEdge_IntAttribute_Snippets_ReturnsCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var options = new RazorCompletionOptions(SnippetsSupported: true);
            var context = CreateRazorCompletionContext(absoluteIndex: 36 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1 />", isRazorFile: false, options, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion =>
                {
                    Assert.Equal("bool-val", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.MinimizedAttributeCommitCharacters, completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                    Assert.False(completion.IsSnippet);
                },
                completion =>
                {
                    Assert.Equal("int-val=\"$0\"", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.AttributeSnippetCommitCharacters, completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                    Assert.True(completion.IsSnippet);
                });
        }

        [Fact]
        public void GetCompletionAt_KnownHtmlElement_ReturnsCompletions_DefaultPriority()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 36 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<title  mutator />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion =>
                {
                    Assert.Equal("Extra", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.MinimizedAttributeCommitCharacters, completion.CommitCharacters);
                    Assert.Equal("Extra", completion.SortText);
                });
        }

        [Fact]
        public void GetCompletionAt_InBody_ReturnsCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 37 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2><</test2>", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion =>
                {
                    Assert.Equal("test1", completion.InsertText);
                },
                completion =>
                {
                    Assert.Equal("test2", completion.InsertText);
                });
        }

        [Fact]
        public void GetCompletionAt_InBody_ParentRequiring_ReturnsCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 37 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test1><</test1>", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion =>
                {
                    Assert.Equal("test1", completion.InsertText);
                },
                completion =>
                {
                    Assert.Equal("SomeChild", completion.InsertText);
                },
                completion =>
                {
                    Assert.Equal("test2", completion.InsertText);
                });
        }

        [Fact]
        public void GetCompletionAt_AtAttributeEdge_BoolAttribute_ReturnsCompletionsWithout()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 36 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2 />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion =>
                {
                    Assert.Equal("bool-val", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.MinimizedAttributeCommitCharacters, completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                },
                completion =>
                {
                    Assert.Equal("int-val", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.AttributeCommitCharacters, completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                });
        }

        [Fact]
        public void GetCompletionAt_AtAttributeEdge_IndexerBoolAttribute_ReturnsCompletionsWithDifferentCommitCharacters()
        {
            // Arrange
            var tagHelper = TagHelperDescriptorBuilder.Create("TestTagHelper", "TestAssembly");
            tagHelper.TagMatchingRule(rule => rule.TagName = "test");
            tagHelper.SetTypeName("TestTagHelper");
            tagHelper.BindAttribute(attribute =>
            {
                attribute.Name = "bool-val";
                attribute.SetPropertyName("BoolVal");
                attribute.TypeName = "System.Collections.Generic.IDictionary<System.String, System.Boolean>";
                attribute.AsDictionary("bool-val-", typeof(bool).FullName);
            });
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 35 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test />", isRazorFile: false, tagHelpers: tagHelper.Build());

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion =>
                {
                    Assert.Equal("bool-val", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.AttributeCommitCharacters, completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                },
                completion =>
                {
                    Assert.Equal("bool-val-", completion.InsertText);
                    Assert.Empty(completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                });
        }

        [Fact]
        public void GetCompletionAt_AtAttributeEdge_IndexerAttribute_ReturnsCompletions()
        {
            // Arrange
            var tagHelper = TagHelperDescriptorBuilder.Create("TestTagHelper", "TestAssembly");
            tagHelper.TagMatchingRule(rule => rule.TagName = "test");
            tagHelper.SetTypeName("TestTagHelper");
            tagHelper.BindAttribute(attribute =>
            {
                attribute.Name = "int-val";
                attribute.SetPropertyName("IntVal");
                attribute.TypeName = "System.Collections.Generic.IDictionary<System.String, System.Int32>";
                attribute.AsDictionary("int-val-", typeof(int).FullName);
            });
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 35 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test />", isRazorFile: false, tagHelpers: tagHelper.Build());

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Collection(
                completions,
                completion =>
                {
                    Assert.Equal("int-val", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.AttributeCommitCharacters, completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                },
                completion =>
                {
                    Assert.Equal("int-val-", completion.InsertText);
                    Assert.Empty(completion.CommitCharacters);
                    Assert.Equal(CompletionSortTextHelper.HighSortPriority, completion.SortText);
                });
        }

        [Fact]
        public void GetCompletionAt_MinimizedAttributeEdge_ReturnsNoCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 38 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2 unbound />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionAt_MinimizedTagHelperAttributeEdge_ReturnsNoCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 38 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2 bool-val />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionAt_InHtmlAttributeName_ReturnsNoCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 38 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2 class='' />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionAt_InTagHelperAttribute_ReturnsNoCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 38 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2 int-val='123' />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        [WorkItem("https://github.com/dotnet/razor-tooling/issues/6134")]
        public void GetCompletionAt_InPossibePartiallyWrittenTagHelper_ReturnsCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 40 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2 int />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            // "bool-var" technically should not be here, but in real-world scenarios it will be filtered by the IDE
            AssertBoolIntCompletions(completions);
        }

        [Fact]
        public void GetCompletionsAt_MalformedAttributeValueInName_ReturnsNoCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2 int-val='>";
            var context = CreateRazorCompletionContext(absoluteIndex: 38 + Environment.NewLine.Length, txt, isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionsAt_MalformedAttributeNamePrefix_ReturnsCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 36 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2 int->", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            AssertBoolIntCompletions(completions);
        }

        [Fact]
        public void GetCompletionAt_HtmlAttributeValue_DoesNotReturnCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var context = CreateRazorCompletionContext(absoluteIndex: 43 + Environment.NewLine.Length, $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2 class='' />", isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionsAt_AttributePrefix_ReturnsCompletions()
        {
            // Arrange
            var service = new TagHelperCompletionProvider(RazorTagHelperCompletionService, HtmlFactsService, TagHelperFactsService);
            var txt = $"@addTagHelper *, TestAssembly{Environment.NewLine}<test2        class=''>";
            var context = CreateRazorCompletionContext(absoluteIndex: 38 + Environment.NewLine.Length, txt, isRazorFile: false, tagHelpers: DefaultTagHelpers);

            // Act
            var completions = service.GetCompletionItems(context);

            // Assert
            AssertBoolIntCompletions(completions);
        }

        private static void AssertBoolIntCompletions(IReadOnlyList<RazorCompletionItem> completions)
        {
            Assert.Collection(completions,
                completion =>
                {
                    Assert.Equal("bool-val", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.MinimizedAttributeCommitCharacters, completion.CommitCharacters);
                },
                completion =>
                {
                    Assert.Equal("int-val", completion.InsertText);
                    Assert.Equal(TagHelperCompletionProvider.AttributeCommitCharacters, completion.CommitCharacters);
                }
            );
        }

        private static RazorCompletionContext CreateRazorCompletionContext(int absoluteIndex, string documentContent, bool isRazorFile, RazorCompletionOptions options = default, params TagHelperDescriptor[] tagHelpers)
        {
            var codeDocument = CreateCodeDocument(documentContent, isRazorFile, tagHelpers);
            var syntaxTree = codeDocument.GetSyntaxTree();
            var tagHelperDocumentContext = codeDocument.GetTagHelperContext();

            var queryableChange = new SourceChange(absoluteIndex, length: 0, newText: string.Empty);
            var owner = syntaxTree.Root.LocateOwner(queryableChange);
            return new RazorCompletionContext(absoluteIndex, owner, syntaxTree, tagHelperDocumentContext, Options: options);
        }
    }
}
