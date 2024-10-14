﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.NET.Sdk.Razor.SourceGenerators;
using Xunit;
using static Microsoft.CodeAnalysis.Razor.RazorProjectEngineBuilderExtensions;

namespace Microsoft.CodeAnalysis.Razor;

public class RazorProjectEngineBuilderExtensionsTest
{
    [Fact]
    public void SetCSharpLanguageVersion_ResolvesNonNumericCSharpLangVersions()
    {
        // Arrange
        var csharpLanguageVersion = CSharp.LanguageVersion.Latest;

        // Act
        var projectEngine = RazorProjectEngine.Create(builder =>
        {
            builder.SetCSharpLanguageVersion(csharpLanguageVersion);
            builder.Features.Add(new ConfigureRazorParserOptions(useRoslynTokenizer: true, CSharpParseOptions.Default.WithLanguageVersion(csharpLanguageVersion)));
        });

        // Assert
        var feature = projectEngine.EngineFeatures.OfType<ConfigureParserForCSharpVersionFeature>().FirstOrDefault();
        Assert.NotNull(feature);
        Assert.NotEqual(csharpLanguageVersion, feature.CSharpLanguageVersion);
    }
}
