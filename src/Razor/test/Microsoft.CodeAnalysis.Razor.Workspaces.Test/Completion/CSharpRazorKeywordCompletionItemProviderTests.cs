// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Completion;

public class CSharpRazorKeywordCompletionItemProviderTests(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    private static readonly Action<RazorCompletionItem>[] s_csharpRazorpKeywordCollectionVerifiers = GetKeywordVerifies(CSharpRazorKeywordCompletionItemProvider.CSharpRazorKeywords);

    [Fact]
    public void GetCSharpRazorKeywordCompletionItems_ReturnsAllCSharpRazorKeywords()
    {
        // Act
        var completionItems = CSharpRazorKeywordCompletionItemProvider.GetCSharpRazorKeywordCompletionItems();

        // Assert
        Assert.Collection(
            completionItems,
            s_csharpRazorpKeywordCollectionVerifiers
        );
    }

    private static Action<RazorCompletionItem>[] GetKeywordVerifies(ImmutableArray<string> keywords)
    {
        using var builder = new PooledArrayBuilder<Action<RazorCompletionItem>>(keywords.Length);

        foreach (var keyword in keywords)
        {
            builder.Add(item => AssertRazorCompletionItem(keyword, item));
        }

        return builder.ToArray();
    }

    private static void AssertRazorCompletionItem(string keyword, RazorCompletionItem item)
    {
        Assert.Equal(keyword, item.InsertText);
        Assert.Equal(keyword, item.DisplayText);

        var completionDescription = Assert.IsType<CSharpRazorKeywordCompletionDescription>(item.DescriptionInfo);
        Assert.Equal(keyword + " Keyword", completionDescription.Description);

        Assert.Equal(CSharpRazorKeywordCompletionItemProvider.KeywordCommitCharacters, item.CommitCharacters);
    }
}
