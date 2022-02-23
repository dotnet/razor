﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;
using Xunit;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Semantic;

public class SemanticTokensCacheTest
{
    [Fact]
    public void TryGetCachedTokens_ReturnsStoredResults()
    {
        // Arrange
        var semanticTokensCache = GetSemanticTokensCache();
        var uri = GetDocumentUri(Path.Join("C:\\", "path", "file.razor"));
        var semanticVersion = new VersionStamp();
        var requestedRange = new Range(startLine: 0, startCharacter: 0, endLine: 7, endCharacter: 0);
        var tokens = new int[] {
            // line, char, length, tokenType, tokenModifiers
            0, 0, 2, 4, 0,
            1, 2, 3, 6, 1,
            4, 0, 10, 50, 5,
        };

        semanticTokensCache.CacheTokens(uri, semanticVersion, requestedRange, tokens);

        // Act
        if (!semanticTokensCache.TryGetCachedTokens(uri, semanticVersion, requestedRange, logger: null, out var cachedResult))
        {
            Assert.True(false, "Cached Tokens were not found");
            throw new NotImplementedException();
        }

        Assert.Equal(tokens, cachedResult.Value.Tokens);
        Assert.Equal(new Range(startLine: 0, startCharacter: 0, endLine: 7, endCharacter: 0), cachedResult.Value.Range);
    }

    [Fact]
    public void TryGetCachedTokens_OmitBegining()
    {
        // Arrange
        var semanticTokensCache = GetSemanticTokensCache();
        var uri = GetDocumentUri(Path.Join("C:\\", "path", "file.razor"));
        var semanticVersion = new VersionStamp();
        var requestedRange = new Range(0, startCharacter: 0, endLine: 10, endCharacter: 0);
        var tokens = new int[] {
            // line, char, length, tokenType, tokenModifiers
            1, 0, 2, 4, 0,
            1, 2, 3, 6, 1,
            4, 0, 10, 50, 5,
        };

        semanticTokensCache.CacheTokens(uri, semanticVersion, requestedRange, tokens);

        // Act
        if (!semanticTokensCache.TryGetCachedTokens(uri, semanticVersion, new Range(startLine: 2, startCharacter: 0, endLine: 8, endCharacter: 0), logger: null, out var cachedResult))
        {
            Assert.True(false, "Cached Tokens were not found");
            throw new NotImplementedException();
        }

        Assert.Equal(new int[] {
            2, 2, 3, 6, 1,
            4, 0, 10, 50, 5,
        }, cachedResult.Value.Tokens);
        Assert.Equal(new Range(startLine: 2, startCharacter: 0, endLine: 8, endCharacter: 0), cachedResult.Value.Range);
    }

    [Fact]
    public void TryGetCachedTokens_OmitEnding()
    {
        // Arrange
        var semanticTokensCache = GetSemanticTokensCache();
        var uri = GetDocumentUri(Path.Join("C:\\", "path", "file.razor"));
        var semanticVersion = new VersionStamp();
        var requestedRange = new Range(startLine: 0, startCharacter: 0, endLine: 5, endCharacter: 0);
        var tokens = new int[] {
            // line, char, length, tokenType, tokenModifiers
            0, 0, 2, 4, 0,
            1, 2, 3, 6, 1,
            4, 0, 10, 50, 5,
        };

        semanticTokensCache.CacheTokens(uri, semanticVersion, requestedRange, tokens);

        // Act
        if (!semanticTokensCache.TryGetCachedTokens(uri, semanticVersion, requestedRange, logger: null, out var cachedResult))
        {
            Assert.True(false, "Cached Tokens were not found");
            throw new NotImplementedException();
        }

        Assert.Equal<int>(tokens.Take(10), cachedResult.Value.Tokens);
        Assert.Equal(new Range(startLine: 0, startCharacter: 0, endLine: 5, endCharacter: 0), cachedResult.Value.Range);
    }

    [Fact]
    public void TryGetCachedTokens_WhenBeginingMissing()
    {
        // Arrange
        var semanticTokensCache = GetSemanticTokensCache();
        var uri = GetDocumentUri(Path.Join("C:\\", "path", "file.razor"));
        var semanticVersion = new VersionStamp();

        semanticTokensCache.CacheTokens(uri, semanticVersion, new Range(startLine: 4, startCharacter: 0, endLine: 5, endCharacter: 0), new int[] { 4, 5, 6, 7, 0, });

        // Act
        if (!semanticTokensCache.TryGetCachedTokens(uri, semanticVersion, new Range(startLine: 0, startCharacter: 0, endLine: 9, endCharacter: 0), logger: null, out var cachedResult))
        {
            Assert.True(false, "Cached Tokens were not found");
            throw new NotImplementedException();
        }

        Assert.Equal(new[] { 4, 5, 6, 7, 0, }, cachedResult.Value.Tokens);

        // If there's a gap between any of our results only take the first (complete).
        Assert.Equal(new Range(4, 0, 5, 0), cachedResult.Value.Range);
    }

    [Fact]
    public void TryGetCachedTokens_PastEndOfFile()
    {
        // Arrange
        var semanticTokensCache = GetSemanticTokensCache();
        var uri = GetDocumentUri(Path.Join("C:\\", "path", "file.razor"));
        var semanticVersion = new VersionStamp();

        semanticTokensCache.CacheTokens(uri, semanticVersion, new Range(startLine: 4, startCharacter: 0, endLine: 4, endCharacter: 20), new int[] { 4, 5, 6, 7, 0, });

        // Act
        if (semanticTokensCache.TryGetCachedTokens(uri, semanticVersion, new Range(startLine: 6, startCharacter: 0, endLine: 8, endCharacter: 20), logger: null, out var _))
        {
            Assert.True(false, "Cached Tokens were found but should not have been.");
            throw new NotImplementedException();
        }
    }

    [Fact]
    public void TryGetCachedTokens_MultipleNonContiguousMissingLines()
    {
        // Arrange
        var semanticTokensCache = GetSemanticTokensCache();
        var uri = GetDocumentUri(Path.Join("C:\\", "path", "file.razor"));
        var semanticVersion = new VersionStamp();
        var tokens = new int[] {
            // line, char, length, tokenType, tokenModifiers
            0, 0, 2, 4, 0,
            1, 2, 3, 6, 1,
            1, 3, 4, 5, 0,
            1, 4, 5, 6, 0,
            1, 5, 6, 7, 0,
            1, 6, 7, 8, 0,
        };

        semanticTokensCache.CacheTokens(uri, semanticVersion, new Range(startLine: 0, startCharacter: 0, endLine: 1, endCharacter: 0), tokens.Take(5).ToArray());
        semanticTokensCache.CacheTokens(uri, semanticVersion, new Range(startLine: 2, startCharacter: 0, endLine: 3, endCharacter: 0), new int[] { 2, 3, 4, 5, 0, });
        semanticTokensCache.CacheTokens(uri, semanticVersion, new Range(startLine: 4, startCharacter: 0, endLine: 5, endCharacter: 0), new int[] { 4, 5, 6, 7, 0, });

        // Act
        if (!semanticTokensCache.TryGetCachedTokens(uri, semanticVersion, new Range(startLine: 0, startCharacter: 0, endLine: 9, endCharacter: 0), logger: null, out var cachedResult))
        {
            Assert.True(false, "Cached Tokens were not found");
            throw new NotImplementedException();
        }

        Assert.Equal(tokens.Take(5), cachedResult.Value.Tokens);

        // If there's a gap between any of our results only take the first (complete) range.
        Assert.Equal(new Range(startLine: 0, startCharacter: 0, endLine: 1, endCharacter: 0), cachedResult.Value.Range);
    }

    private static DocumentUri GetDocumentUri(string path)
    {
        return new DocumentUri(
            scheme: null,
            authority: null,
            path: path,
            query: null,
            fragment: null);
    }

    private static SemanticTokensCache GetSemanticTokensCache()
    {
        return new SemanticTokensCache();
    }
}
