// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudioCode.RazorExtension.Services;
using Xunit;

public class MappingServiceTests
{
    private readonly MappingService _mappingService;

    public MappingServiceTests()
    {
        _mappingService = new MappingService(new TestRazorClientManagerService());
    }

    [Fact]
    public async Task MapSpansAsync_EmptyFilePath_ReturnsEmptyArray()
    {
        // Arrange
        using var workspace = TestWorkspace.Create(w =>
        {
            var project = w.AddProject("TestProject", LanguageNames.CSharp);
            w.AddDocument(project.Id, "TestDocument.razor", SourceText.From(""));
        });

        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        document = document.WithFilePath(null);
        var spans = new[] { TextSpan.FromBounds(0, 5) };

        // Act
        var result = await _mappingService.MapSpansAsync(document, spans, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task MapSpansAsync_NoSpans_ReturnsEmptyArray()
    {
        // Arrange
        using var workspace = TestWorkspace.Create(w =>
        {
            var project = w.AddProject("TestProject", LanguageNames.CSharp);
            w.AddDocument(project.Id, "TestDocument.razor", SourceText.From("Hello World"));
        });

        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var spans = Array.Empty<TextSpan>();

        // Act
        var result = await _mappingService.MapSpansAsync(document, spans, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }
}
