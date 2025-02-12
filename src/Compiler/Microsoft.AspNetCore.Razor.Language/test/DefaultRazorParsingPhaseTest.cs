// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class DefaultRazorParsingPhaseTest
{
    [Fact]
    public void Execute_AddsSyntaxTree()
    {
        // Arrange
        var phase = new DefaultRazorParsingPhase();
        var engine = RazorProjectEngine.CreateEmpty(builder =>
        {
            builder.Phases.Add(phase);
            builder.Features.Add(new DefaultRazorParserOptionsFeature());
        });

        var codeDocument = TestRazorCodeDocument.CreateEmpty();

        // Act
        phase.Execute(codeDocument);

        // Assert
        Assert.NotNull(codeDocument.GetSyntaxTree());
    }

    [Fact]
    public void Execute_UsesConfigureParserFeatures()
    {
        // Arrange
        var phase = new DefaultRazorParsingPhase();
        var engine = RazorProjectEngine.CreateEmpty((builder) =>
        {
            builder.Phases.Add(phase);
            builder.Features.Add(new DefaultRazorParserOptionsFeature());
            builder.ConfigureParserOptions(ConfigureDirective);
        });

        var codeDocument = TestRazorCodeDocument.CreateEmpty();

        // Act
        phase.Execute(codeDocument);

        // Assert
        var syntaxTree = codeDocument.GetSyntaxTree();
        var directive = Assert.Single(syntaxTree.Options.Directives);
        Assert.Equal("test", directive.Directive);
    }

    [Fact]
    public void Execute_ParsesImports()
    {
        // Arrange
        var phase = new DefaultRazorParsingPhase();
        var engine = RazorProjectEngine.CreateEmpty((builder) =>
        {
            builder.Phases.Add(phase);
            builder.Features.Add(new DefaultRazorParserOptionsFeature());
            builder.ConfigureParserOptions(ConfigureDirective);
        });

        var imports = ImmutableArray.Create(
            TestRazorSourceDocument.Create(),
            TestRazorSourceDocument.Create());

        var codeDocument = TestRazorCodeDocument.Create(TestRazorSourceDocument.Create(), imports);

        // Act
        phase.Execute(codeDocument);

        // Assert
        var importSyntaxTrees = codeDocument.GetImportSyntaxTrees();
        Assert.False(importSyntaxTrees.IsDefault);
        Assert.Collection(
            importSyntaxTrees,
            t => { Assert.Same(t.Source, imports[0]); Assert.Equal("test", Assert.Single(t.Options.Directives).Directive); },
            t => { Assert.Same(t.Source, imports[1]); Assert.Equal("test", Assert.Single(t.Options.Directives).Directive); });
    }

    private static void ConfigureDirective(RazorParserOptionsBuilder builder)
    {
        builder.Directives = [DirectiveDescriptor.CreateDirective("test", DirectiveKind.SingleLine)];
    }
}
