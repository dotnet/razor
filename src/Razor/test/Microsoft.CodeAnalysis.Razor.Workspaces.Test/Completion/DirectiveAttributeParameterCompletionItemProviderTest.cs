// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.IntegrationTests;
using Microsoft.VisualStudio.Editor.Razor;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    public class DirectiveAttributeParameterCompletionItemProviderTest : RazorIntegrationTestBase
    {
        internal override string FileKind => FileKinds.Component;

        internal override bool UseTwoPhaseCompilation => true;

        public DirectiveAttributeParameterCompletionItemProviderTest()
        {
            Provider = new DirectiveAttributeParameterCompletionItemProvider(new DefaultTagHelperFactsService());
            EmptyAttributes = Enumerable.Empty<string>();

            // Most of these completions rely on stuff in the web namespace.
            ImportItems.Add(CreateProjectItem(
                "_Imports.razor",
                "@using Microsoft.AspNetCore.Components.Web"));

            var codeDocument = GetCodeDocument(string.Empty);
            DefaultTagHelperDocumentContext = codeDocument.GetTagHelperContext();
        }

        private DirectiveAttributeParameterCompletionItemProvider Provider { get; }

        private TagHelperDocumentContext DefaultTagHelperDocumentContext { get; }

        private IEnumerable<string> EmptyAttributes { get; }

        private RazorCodeDocument GetCodeDocument(string content)
        {
            var result = CompileToCSharp(content, throwOnFailure: false);
            return result.CodeDocument;
        }

        [Fact]
        public void GetCompletionItems_LocationHasNoOwner_ReturnsEmptyCollection()
        {
            // Arrange
            var context = CreateRazorCompletionContext(absoluteIndex: 30, "<input @  />");

            // Act
            var completions = Provider.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionItems_OnNonAttributeArea_ReturnsEmptyCollection()
        {
            // Arrange
            var context = CreateRazorCompletionContext(absoluteIndex: 3, "<input @  />");

            // Act
            var completions = Provider.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionItems_OnDirectiveAttributeName_ReturnsEmptyCollection()
        {
            // Arrange
            var context = CreateRazorCompletionContext(absoluteIndex: 8, "<input @bind:fo  />");

            // Act
            var completions = Provider.GetCompletionItems(context);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetCompletionItems_OnDirectiveAttributeParameter_ReturnsCompletions()
        {
            // Arrange
            var context = CreateRazorCompletionContext(absoluteIndex: 14, "<input @bind:fo  />");

            // Act
            var completions = Provider.GetCompletionItems(context);

            // Assert
            Assert.Equal(6, completions.Count);
            AssertContains(completions, "culture");
            AssertContains(completions, "event");
            AssertContains(completions, "format");
            AssertContains(completions, "get");
            AssertContains(completions, "set");
            AssertContains(completions, "after");
        }

        [Fact]
        public void GetAttributeParameterCompletions_NoDescriptorsForTag_ReturnsEmptyCollection()
        {
            // Arrange
            var documentContext = TagHelperDocumentContext.Create(string.Empty, Enumerable.Empty<TagHelperDescriptor>());

            // Act
            var completions = Provider.GetAttributeParameterCompletions("@bin", string.Empty, "foobarbaz", EmptyAttributes, documentContext);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetAttributeParameterCompletions_NoDirectiveAttributesForTag_ReturnsEmptyCollection()
        {
            // Arrange
            var descriptor = TagHelperDescriptorBuilder.Create("CatchAll", "TestAssembly");
            descriptor.BoundAttributeDescriptor(boundAttribute => boundAttribute.Name = "Test");
            descriptor.TagMatchingRule(rule => rule.RequireTagName("*"));
            var documentContext = TagHelperDocumentContext.Create(string.Empty, new[] { descriptor.Build() });

            // Act
            var completions = Provider.GetAttributeParameterCompletions("@bin", string.Empty, "input", EmptyAttributes, documentContext);

            // Assert
            Assert.Empty(completions);
        }

        [Fact]
        public void GetAttributeParameterCompletions_SelectedDirectiveAttributeParameter_IsExcludedInCompletions()
        {
            // Arrange
            var attributeNames = new string[] { "@bind" };

            // Act
            var completions = Provider.GetAttributeParameterCompletions("@bind", "format", "input", attributeNames, DefaultTagHelperDocumentContext);

            // Assert
            AssertDoesNotContain(completions, "format");
        }

        [Fact]
        public void GetAttributeParameterCompletions_ReturnsCompletion()
        {
            // Arrange

            // Act
            var completions = Provider.GetAttributeParameterCompletions("@bind", string.Empty, "input", EmptyAttributes, DefaultTagHelperDocumentContext);

            // Assert
            AssertContains(completions, "format");
        }

        [Fact]
        public void GetAttributeParameterCompletions_BaseDirectiveAttributeAndParameterVariationsExist_ExcludesCompletion()
        {
            // Arrange
            var attributeNames = new[]
            {
                "@bind",
                "@bind:format",
                "@bind:event",
                "@",
            };

            // Act
            var completions = Provider.GetAttributeParameterCompletions("@bind", string.Empty, "input", attributeNames, DefaultTagHelperDocumentContext);

            // Assert
            AssertDoesNotContain(completions, "format");
        }

        private static void AssertContains(IReadOnlyList<RazorCompletionItem> completions, string insertText)
        {
            Assert.Contains(completions, completion => insertText == completion.InsertText &&
                    insertText == completion.DisplayText &&
                    RazorCompletionItemKind.DirectiveAttributeParameter == completion.Kind);
        }

        private static void AssertDoesNotContain(IReadOnlyList<RazorCompletionItem> completions, string insertText)
        {

            Assert.DoesNotContain(completions, completion => insertText == completion.InsertText &&
                   insertText == completion.DisplayText &&
                   RazorCompletionItemKind.DirectiveAttributeParameter == completion.Kind);
        }

        private RazorCompletionContext CreateRazorCompletionContext(int absoluteIndex, string documentContent)
        {
            var codeDocument = GetCodeDocument(documentContent);
            var syntaxTree = codeDocument.GetSyntaxTree();
            var tagHelperDocumentContext = codeDocument.GetTagHelperContext();

            var queryableChange = new SourceChange(absoluteIndex, length: 0, newText: string.Empty);
            var owner = syntaxTree.Root.LocateOwner(queryableChange);
            return new RazorCompletionContext(absoluteIndex, owner, syntaxTree, tagHelperDocumentContext);
        }
    }
}
