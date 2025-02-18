// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class PageDirectiveTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_3_0;

    protected override void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
        PageDirective.Register(builder);
    }

    protected override void ConfigureProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.RunPhasesTo<IRazorDocumentClassifierPhase>();
    }

    [Fact]
    public void TryGetPageDirective_ReturnsTrue_IfPageIsMalformed()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("@page \"some-route-template\" Invalid");
        var documentNode = processor.GetDocumentNode();

        // Act
        Assert.True(PageDirective.TryGetPageDirective(documentNode, out var pageDirective));

        // Assert
        Assert.Equal("some-route-template", pageDirective.RouteTemplate);
        Assert.NotNull(pageDirective.DirectiveNode);
    }

    [Fact]
    public void TryGetPageDirective_ReturnsTrue_IfPageIsImported()
    {
        // Arrange
        var importSource = RazorSourceDocument.Create("@page", "import.cshtml");
        var processor = CreateAndInitializeCodeDocument("Hello world", [importSource]);
        var documentNode = processor.GetDocumentNode();

        // Act
        Assert.True(PageDirective.TryGetPageDirective(documentNode, out var pageDirective));

        // Assert
        Assert.Null(pageDirective.RouteTemplate);
    }

    [Fact]
    public void TryGetPageDirective_ReturnsFalse_IfPageDoesNotHaveDirective()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("Hello world");
        var documentNode = processor.GetDocumentNode();

        // Act & Assert
        Assert.False(PageDirective.TryGetPageDirective(documentNode, out _));
    }

    [Fact]
    public void TryGetPageDirective_ReturnsTrue_IfPageDoesStartWithDirective()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("Hello @page");
        var documentNode = processor.GetDocumentNode();

        // Act
        Assert.True(PageDirective.TryGetPageDirective(documentNode, out var pageDirective));

        // Assert
        Assert.Null(pageDirective.RouteTemplate);
        Assert.NotNull(pageDirective.DirectiveNode);
    }

    [Fact]
    public void TryGetPageDirective_ReturnsTrue_IfContentHasDirective()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("@page");
        var documentNode = processor.GetDocumentNode();

        // Act
        Assert.True(PageDirective.TryGetPageDirective(documentNode, out var pageDirective));

        // Assert
        Assert.Null(pageDirective.RouteTemplate);
    }

    [Fact]
    public void TryGetPageDirective_ParsesRouteTemplate()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("@page \"some-route-template\"");
        var documentNode = processor.GetDocumentNode();

        // Act
        Assert.True(PageDirective.TryGetPageDirective(documentNode, out var pageDirective));

        // Assert
        Assert.Equal("some-route-template", pageDirective.RouteTemplate);
    }
}
