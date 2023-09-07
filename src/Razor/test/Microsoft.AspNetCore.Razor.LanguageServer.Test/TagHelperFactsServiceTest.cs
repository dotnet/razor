﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

public class TagHelperFactsServiceTest(ITestOutputHelper testOutput) : TagHelperServiceTestBase(testOutput)
{
    [Fact]
    public void StringifyAttributes_DirectiveAttribute()
    {
        // Arrange
        var codeDocument = CreateComponentDocument($"<TestElement @test='abc' />", DefaultTagHelpers);
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceSpan = new SourceSpan(3, 0);
        var sourceChangeLocation = new SourceChange(sourceSpan, string.Empty);
        var startTag = (MarkupTagHelperStartTagSyntax)syntaxTree.Root.LocateOwner(sourceChangeLocation).Parent;

        // Act
        var attributes = TagHelperFactsService.StringifyAttributes(startTag.Attributes);

        // Assert
        Assert.Collection(
            attributes,
            attribute =>
            {
                Assert.Equal("@test", attribute.Key);
                Assert.Equal("abc", attribute.Value);
            });
    }

    [Fact]
    public void StringifyAttributes_DirectiveAttributeWithParameter()
    {
        // Arrange
        var codeDocument = CreateComponentDocument($"<TestElement @test:something='abc' />", DefaultTagHelpers);
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceSpan = new SourceSpan(3, 0);
        var sourceChangeLocation = new SourceChange(sourceSpan, string.Empty);
        var startTag = (MarkupTagHelperStartTagSyntax)syntaxTree.Root.LocateOwner(sourceChangeLocation).Parent;

        // Act
        var attributes = TagHelperFactsService.StringifyAttributes(startTag.Attributes);

        // Assert
        Assert.Collection(
            attributes,
            attribute =>
            {
                Assert.Equal("@test:something", attribute.Key);
                Assert.Equal("abc", attribute.Value);
            });
    }

    [Fact]
    public void StringifyAttributes_MinimizedDirectiveAttribute()
    {
        // Arrange
        var codeDocument = CreateComponentDocument($"<TestElement @minimized />", DefaultTagHelpers);
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceSpan = new SourceSpan(3, 0);
        var sourceChangeLocation = new SourceChange(sourceSpan, string.Empty);
        var startTag = (MarkupTagHelperStartTagSyntax)syntaxTree.Root.LocateOwner(sourceChangeLocation).Parent;

        // Act
        var attributes = TagHelperFactsService.StringifyAttributes(startTag.Attributes);

        // Assert
        Assert.Collection(
            attributes,
            attribute =>
            {
                Assert.Equal("@minimized", attribute.Key);
                Assert.Equal(string.Empty, attribute.Value);
            });
    }

    [Fact]
    public void StringifyAttributes_MinimizedDirectiveAttributeWithParameter()
    {
        // Arrange
        var codeDocument = CreateComponentDocument($"<TestElement @minimized:something />", DefaultTagHelpers);
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceSpan = new SourceSpan(3, 0);
        var sourceChangeLocation = new SourceChange(sourceSpan, string.Empty);
        var startTag = (MarkupTagHelperStartTagSyntax)syntaxTree.Root.LocateOwner(sourceChangeLocation).Parent;

        // Act
        var attributes = TagHelperFactsService.StringifyAttributes(startTag.Attributes);

        // Assert
        Assert.Collection(
            attributes,
            attribute =>
            {
                Assert.Equal("@minimized:something", attribute.Key);
                Assert.Equal(string.Empty, attribute.Value);
            });
    }

    [Fact]
    public void StringifyAttributes_TagHelperAttribute()
    {
        // Arrange
        var tagHelper = TagHelperDescriptorBuilder.Create("WithBoundAttribute", "TestAssembly");
        tagHelper.TagMatchingRule(rule => rule.TagName = "test");
        tagHelper.BindAttribute(attribute =>
        {
            attribute.Name = "bound";
            attribute.SetMetadata(PropertyName("Bound"));
            attribute.TypeName = typeof(bool).FullName;
        });
        tagHelper.SetMetadata(TypeName("WithBoundAttribute"));
        var codeDocument = CreateCodeDocument($"@addTagHelper *, TestAssembly{Environment.NewLine}<test bound='true' />", isRazorFile: false, tagHelper.Build());
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceSpan = new SourceSpan(30 + Environment.NewLine.Length, 0);
        var sourceChangeLocation = new SourceChange(sourceSpan, string.Empty);
        var startTag = (MarkupTagHelperStartTagSyntax)syntaxTree.Root.LocateOwner(sourceChangeLocation).Parent;

        // Act
        var attributes = TagHelperFactsService.StringifyAttributes(startTag.Attributes);

        // Assert
        Assert.Collection(
            attributes,
            attribute =>
            {
                Assert.Equal("bound", attribute.Key);
                Assert.Equal("true", attribute.Value);
            });
    }

    [Fact]
    public void StringifyAttributes_MinimizedTagHelperAttribute()
    {
        // Arrange
        var tagHelper = TagHelperDescriptorBuilder.Create("WithBoundAttribute", "TestAssembly");
        tagHelper.TagMatchingRule(rule => rule.TagName = "test");
        tagHelper.BindAttribute(attribute =>
        {
            attribute.Name = "bound";
            attribute.SetMetadata(PropertyName("Bound"));
            attribute.TypeName = typeof(bool).FullName;
        });
        tagHelper.SetMetadata(TypeName("WithBoundAttribute"));
        var codeDocument = CreateCodeDocument($"@addTagHelper *, TestAssembly{Environment.NewLine}<test bound />", isRazorFile: false, tagHelper.Build());
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceSpan = new SourceSpan(30 + Environment.NewLine.Length, 0);
        var sourceChangeLocation = new SourceChange(sourceSpan, string.Empty);
        var startTag = (MarkupTagHelperStartTagSyntax)syntaxTree.Root.LocateOwner(sourceChangeLocation).Parent;

        // Act
        var attributes = TagHelperFactsService.StringifyAttributes(startTag.Attributes);

        // Assert
        Assert.Collection(
            attributes,
            attribute =>
            {
                Assert.Equal("bound", attribute.Key);
                Assert.Equal(string.Empty, attribute.Value);
            });
    }

    [Fact]
    public void StringifyAttributes_UnboundAttribute()
    {
        // Arrange
        var codeDocument = CreateCodeDocument($"@addTagHelper *, TestAssembly{Environment.NewLine}<input unbound='hello world' />", isRazorFile: false, DefaultTagHelpers);
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceSpan = new SourceSpan(30 + Environment.NewLine.Length, 0);
        var sourceChangeLocation = new SourceChange(sourceSpan, string.Empty);
        var startTag = (MarkupStartTagSyntax)syntaxTree.Root.LocateOwner(sourceChangeLocation).Parent;

        // Act
        var attributes = TagHelperFactsService.StringifyAttributes(startTag.Attributes);

        // Assert
        Assert.Collection(
            attributes,
            attribute =>
            {
                Assert.Equal("unbound", attribute.Key);
                Assert.Equal("hello world", attribute.Value);
            });
    }

    [Fact]
    public void StringifyAttributes_UnboundMinimizedAttribute()
    {
        // Arrange
        var codeDocument = CreateCodeDocument($"@addTagHelper *, TestAssembly{Environment.NewLine}<input unbound />", isRazorFile: false, DefaultTagHelpers);
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceSpan = new SourceSpan(30 + Environment.NewLine.Length, 0);
        var sourceChangeLocation = new SourceChange(sourceSpan, string.Empty);
        var startTag = (MarkupStartTagSyntax)syntaxTree.Root.LocateOwner(sourceChangeLocation).Parent;

        // Act
        var attributes = TagHelperFactsService.StringifyAttributes(startTag.Attributes);

        // Assert
        Assert.Collection(
            attributes,
            attribute =>
            {
                Assert.Equal("unbound", attribute.Key);
                Assert.Equal(string.Empty, attribute.Value);
            });
    }

    [Fact]
    public void StringifyAttributes_IgnoresMiscContent()
    {
        // Arrange
        var codeDocument = CreateCodeDocument($"@addTagHelper *, TestAssembly{Environment.NewLine}<input unbound @DateTime.Now />", isRazorFile: false, DefaultTagHelpers);
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceSpan = new SourceSpan(30 + Environment.NewLine.Length, 0);
        var sourceChangeLocation = new SourceChange(sourceSpan, string.Empty);
        var startTag = (MarkupStartTagSyntax)syntaxTree.Root.LocateOwner(sourceChangeLocation).Parent;

        // Act
        var attributes = TagHelperFactsService.StringifyAttributes(startTag.Attributes);

        // Assert
        Assert.Collection(
            attributes,
            attribute =>
            {
                Assert.Equal("unbound", attribute.Key);
                Assert.Equal(string.Empty, attribute.Value);
            });
    }

    private static RazorCodeDocument CreateComponentDocument(string text, ImmutableArray<TagHelperDescriptor> tagHelpers)
    {
        tagHelpers = tagHelpers.NullToEmpty();
        var sourceDocument = TestRazorSourceDocument.Create(text);
        var projectEngine = RazorProjectEngine.Create(builder => { });
        var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, FileKinds.Component, Array.Empty<RazorSourceDocument>(), tagHelpers);
        return codeDocument;
    }
}
