// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Razor.LanguageClient.Cohost;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.RazorExtension.Test.Endpoints.Shared;

public class ComputedTargetPathTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task GetHintName()
    {
        // Creating a misc files project will mean that there is no globalconfig created, so no target paths will be set
        var document = CreateProjectAndRazorDocument("");

        _ = await document.Project.GetCompilationAsync(DisposalToken);

        Assert.True(document.TryComputeHintNameFromRazorDocument(out var hintName));
        var generatedDocument = await document.Project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, DisposalToken);
        Assert.NotNull(generatedDocument);
    }

    [Fact]
    public async Task NoGlobalConfig_WithProjectFilePath()
    {
        var doc1Path = FilePath(@"Pages\Index.razor");

        var document = CreateProjectAndRazorDocument("");

        var doc1 = document.Project.AddAdditionalDocument(
            doc1Path,
            SourceText.From("""
                <div>This is a page</div>
                """),
            filePath: doc1Path);

        var project = doc1.Project.RemoveAnalyzerConfigDocument(doc1.Project.AnalyzerConfigDocuments.First().Id);

        _ = await project.GetCompilationAsync(DisposalToken);

        doc1 = project.GetAdditionalDocument(doc1.Id).AssumeNotNull();

        Assert.True(doc1.TryComputeHintNameFromRazorDocument(out var hintName));
        var generatedDocument = await doc1.Project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, DisposalToken);
        Assert.NotNull(generatedDocument);
    }

    [Fact]
    public async Task NoGlobalConfig_NoProjectFilePath()
    {
        var doc1Path = FilePath(@"Pages\Index.razor");

        // Creating a misc files project will mean that there is no globalconfig created, so no target paths will be set
        var document = CreateProjectAndRazorDocument("", miscellaneousFile: true);

        var doc1 = document.Project.AddAdditionalDocument(
            doc1Path,
            SourceText.From("""
                <div>This is a page</div>
                """),
            filePath: doc1Path);

        _ = await doc1.Project.GetCompilationAsync(DisposalToken);

        Assert.True(doc1.TryComputeHintNameFromRazorDocument(out var hintName));
        var generatedDocument = await doc1.Project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, DisposalToken);
        Assert.NotNull(generatedDocument);
    }

    [Fact]
    public async Task NoGlobalConfig_MultipleFilesWithTheSameName()
    {
        var doc1Path = FilePath(@"Pages\Index.razor");
        var doc2Path = FilePath(@"Components\Index.razor");

        // Creating a misc files project will mean that there is no globalconfig created, so no target paths will be set
        var document = CreateProjectAndRazorDocument("", miscellaneousFile: true);

        var doc1 = document.Project.AddAdditionalDocument(
            doc1Path,
            SourceText.From("""
                <div>This is a page</div>
                """),
            filePath: doc1Path);
        var doc2 = doc1.Project.AddAdditionalDocument(
            doc2Path,
            SourceText.From("""
                <div>This is a component</div>
                """),
            filePath: doc2Path);

        // Make sure we have a doc1 from the final project
        doc1 = doc2.Project.GetAdditionalDocument(doc1.Id).AssumeNotNull();

        Assert.True(doc1.TryComputeHintNameFromRazorDocument(out var hintName1));
        var generatedDocument = await doc1.Project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName1, DisposalToken);
        Assert.NotNull(generatedDocument);

        Assert.True(doc2.TryComputeHintNameFromRazorDocument(out var hintName2));
        generatedDocument = await doc2.Project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName2, DisposalToken);
        Assert.NotNull(generatedDocument);
    }

    [Fact]
    public async Task NotAllFilesHaveTargetPaths()
    {
        var doc1Path = FilePath(@"Pages\Index.razor");

        // This will create a project with a globalconfig, and target paths for a single razor file
        var document = CreateProjectAndRazorDocument("""
            <div>This is a normal file with a target path
            """);

        // Now add a file without updating the globalconfig
        var doc1 = document.Project.AddAdditionalDocument(
            doc1Path,
            SourceText.From("""
                <div>This is an extra document</div>
                """),
            filePath: doc1Path);

        Assert.True(doc1.TryComputeHintNameFromRazorDocument(out var hintName));
        var generatedDocument = await doc1.Project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, DisposalToken);
        Assert.NotNull(generatedDocument);
    }
}
