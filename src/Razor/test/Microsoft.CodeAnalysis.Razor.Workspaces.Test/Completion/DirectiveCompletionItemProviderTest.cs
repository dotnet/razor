﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    public class DirectiveCompletionItemProviderTest
    {
        private static readonly IReadOnlyList<DirectiveDescriptor> s_defaultDirectives = new[]
        {
            CSharpCodeParser.AddTagHelperDirectiveDescriptor,
            CSharpCodeParser.RemoveTagHelperDirectiveDescriptor,
            CSharpCodeParser.TagHelperPrefixDirectiveDescriptor,
        };

        [Fact]
        public void GetDirectiveCompletionItems_ReturnsDefaultDirectivesAsCompletionItems()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@addTag");

            // Act
            var completionItems = DirectiveCompletionItemProvider.GetDirectiveCompletionItems(syntaxTree);

            // Assert
            Assert.Collection(
                completionItems,
                item => AssertRazorCompletionItem(s_defaultDirectives[0], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[1], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[2], item));
        }

        [Fact]
        public void GetDirectiveCompletionItems_ReturnsCustomDirectivesAsCompletionItems()
        {
            // Arrange
            var customDirective = DirectiveDescriptor.CreateSingleLineDirective("custom", builder => builder.Description = "My Custom Directive.");
            var syntaxTree = CreateSyntaxTree("@addTag", customDirective);

            // Act
            var completionItems = DirectiveCompletionItemProvider.GetDirectiveCompletionItems(syntaxTree);

            // Assert
            Assert.Collection(
                completionItems,
                item => AssertRazorCompletionItem(customDirective, item),
                item => AssertRazorCompletionItem(s_defaultDirectives[0], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[1], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[2], item));
        }

        [Fact]
        public void GetDirectiveCompletionItems_UsesDisplayNamesWhenNotNull()
        {
            // Arrange
            var customDirective = DirectiveDescriptor.CreateSingleLineDirective("custom", builder =>
            {
                builder.DisplayName = "different";
                builder.Description = "My Custom Directive.";
            });
            var syntaxTree = CreateSyntaxTree("@addTag", customDirective);

            // Act
            var completionItems = DirectiveCompletionItemProvider.GetDirectiveCompletionItems(syntaxTree);

            // Assert
            Assert.Collection(
                completionItems,
                item => AssertRazorCompletionItem("different", customDirective, item),
                item => AssertRazorCompletionItem(s_defaultDirectives[0], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[1], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[2], item));
        }

        [Fact]
        public void GetDirectiveCompletionItems_CodeBlockCommitCharacters()
        {
            // Arrange
            var customDirective = DirectiveDescriptor.CreateCodeBlockDirective("custom", builder =>
            {
                builder.DisplayName = "code";
                builder.Description = "My Custom Code Block Directive.";
            });
            var syntaxTree = CreateSyntaxTree("@cod", customDirective);

            // Act
            var completionItems = DirectiveCompletionItemProvider.GetDirectiveCompletionItems(syntaxTree);

            // Assert
            Assert.Collection(
                completionItems,
                item => AssertRazorCompletionItem("code", customDirective, item, DirectiveCompletionItemProvider.BlockDirectiveCommitCharacters),
                item => AssertRazorCompletionItem(s_defaultDirectives[0], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[1], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[2], item));
        }

        [Fact]
        public void GetDirectiveCompletionItems_RazorBlockCommitCharacters()
        {
            // Arrange
            var customDirective = DirectiveDescriptor.CreateRazorBlockDirective("custom", builder =>
            {
                builder.DisplayName = "section";
                builder.Description = "My Custom Razozr Block Directive.";
            });
            var syntaxTree = CreateSyntaxTree("@sec", customDirective);

            // Act
            var completionItems = DirectiveCompletionItemProvider.GetDirectiveCompletionItems(syntaxTree);

            // Assert
            Assert.Collection(
                completionItems,
                item => AssertRazorCompletionItem("section", customDirective, item, DirectiveCompletionItemProvider.BlockDirectiveCommitCharacters),
                item => AssertRazorCompletionItem(s_defaultDirectives[0], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[1], item),
                item => AssertRazorCompletionItem(s_defaultDirectives[2], item));
        }

        [Fact]
        public void GetDirectiveCompletionItems_ComponentDocument_DoesNotReturnsDefaultDirectivesAsCompletionItems()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@addTag", FileKinds.Component);

            // Act
            var completionItems = DirectiveCompletionItemProvider.GetDirectiveCompletionItems(syntaxTree);

            // Assert
            Assert.Empty(completionItems);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseIfSyntaxTreeNull()
        {
            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree: null, location: new SourceSpan(0, 0));

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseIfNoOwner()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@");
            var location = new SourceSpan(2, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseWhenOwnerIsNotExpression()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@{");
            var location = new SourceSpan(2, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseWhenOwnerIsComplexExpression()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@DateTime.Now");
            var location = new SourceSpan(2, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseWhenOwnerIsExplicitExpression()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@(something)");
            var location = new SourceSpan(4, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseWhenInsideStatement()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@{ @ }");
            var location = new SourceSpan(4, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseWhenInsideMarkup()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("<p>@ </p>");
            var location = new SourceSpan(4, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseWhenInsideAttributeArea()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("<p @ >");
            var location = new SourceSpan(4, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseWhenInsideDirective()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@functions { @  }", FunctionsDirective.Directive);
            var location = new SourceSpan(14, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsTrueForSimpleImplicitExpressionsStartOfWord()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@m");
            var location = new SourceSpan(1, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void AtDirectiveCompletionPoint_ReturnsFalseForSimpleImplicitExpressions()
        {
            // Arrange
            var syntaxTree = CreateSyntaxTree("@mod");
            var location = new SourceSpan(2, 0);

            // Act
            var result = DirectiveCompletionItemProvider.AtDirectiveCompletionPoint(syntaxTree, location);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsDirectiveCompletableToken_ReturnsTrueForCSharpIdentifiers()
        {
            // Arrange
            var csharpToken = SyntaxFactory.Token(SyntaxKind.Identifier, "model");

            // Act
            var result = DirectiveCompletionItemProvider.IsDirectiveCompletableToken(csharpToken);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsDirectiveCompletableToken_ReturnsTrueForCSharpMarkerTokens()
        {
            // Arrange
            var csharpToken = SyntaxFactory.Token(SyntaxKind.Marker, string.Empty);

            // Act
            var result = DirectiveCompletionItemProvider.IsDirectiveCompletableToken(csharpToken);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsDirectiveCompletableToken_ReturnsFalseForNonCSharpTokens()
        {
            // Arrange
            var token = SyntaxFactory.Token(SyntaxKind.Text, string.Empty);

            // Act
            var result = DirectiveCompletionItemProvider.IsDirectiveCompletableToken(token);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsDirectiveCompletableToken_ReturnsFalseForInvalidCSharpTokens()
        {
            // Arrange
            var csharpToken = SyntaxFactory.Token(SyntaxKind.Tilde, "~");

            // Act
            var result = DirectiveCompletionItemProvider.IsDirectiveCompletableToken(csharpToken);

            // Assert
            Assert.False(result);
        }

        private static void AssertRazorCompletionItem(string completionDisplayText, DirectiveDescriptor directive, RazorCompletionItem item, IReadOnlyCollection<string> commitCharacters = null)
        {
            Assert.Equal(item.DisplayText, completionDisplayText);
            Assert.Equal(item.InsertText, directive.Directive);
            var completionDescription = item.GetDirectiveCompletionDescription();
            Assert.Equal(directive.Description, completionDescription.Description);
            Assert.Equal(item.CommitCharacters, commitCharacters ?? DirectiveCompletionItemProvider.SingleLineDirectiveCommitCharacters);
        }

        private static void AssertRazorCompletionItem(DirectiveDescriptor directive, RazorCompletionItem item) =>
            AssertRazorCompletionItem(directive.Directive, directive, item);

        private static RazorSyntaxTree CreateSyntaxTree(string text, params DirectiveDescriptor[] directives)
        {
            return CreateSyntaxTree(text, FileKinds.Legacy, directives);
        }

        private static RazorSyntaxTree CreateSyntaxTree(string text, string fileKind, params DirectiveDescriptor[] directives)
        {
            var sourceDocument = TestRazorSourceDocument.Create(text);
            var options = RazorParserOptions.Create(builder =>
            {
                foreach (var directive in directives)
                {
                    builder.Directives.Add(directive);
                }
            }, fileKind);
            var syntaxTree = RazorSyntaxTree.Parse(sourceDocument, options);
            return syntaxTree;
        }
    }
}
