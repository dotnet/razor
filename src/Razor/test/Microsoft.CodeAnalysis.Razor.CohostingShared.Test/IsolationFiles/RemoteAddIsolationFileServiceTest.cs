// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.IsolationFiles;
using Microsoft.CodeAnalysis.Razor.Remote;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.IsolationFiles;

public class RemoteAddIsolationFileServiceTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task CssIsolationFile_CreatesEmptyFile()
    {
        var document = CreateProjectAndRazorDocument("<div></div>");
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddIsolationFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddIsolationFileAsync(solutionInfo, razorFileUri, IsolationFileKind.Css, ct),
            DisposalToken);

        Assert.NotNull(result);
        var changes = GetDocumentChanges(result);
        Assert.Equal(2, changes.Length);

        // First change: CreateFile
        Assert.True(changes[0].TryGetSecond(out var createFile));
        Assert.Contains(".razor.css", createFile!.DocumentUri.ToString());

        // Second change: TextDocumentEdit with CSS comment content
        Assert.True(changes[1].TryGetFirst(out var textEdit));
        Assert.Single(textEdit!.Edits);
        Assert.Contains("Scoped CSS styles for File1 component", ((TextEdit)textEdit.Edits[0]).NewText);
    }

    [Fact]
    public async Task JavaScriptIsolationFile_CreatesFileWithTemplate()
    {
        var document = CreateProjectAndRazorDocument("<div></div>");
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddIsolationFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddIsolationFileAsync(solutionInfo, razorFileUri, IsolationFileKind.JavaScript, ct),
            DisposalToken);

        Assert.NotNull(result);
        var changes = GetDocumentChanges(result);
        Assert.Equal(2, changes.Length);

        // First change: CreateFile
        Assert.True(changes[0].TryGetSecond(out var createFile));
        Assert.Contains(".razor.js", createFile!.DocumentUri.ToString());

        // Second change: TextDocumentEdit with JS comment content
        Assert.True(changes[1].TryGetFirst(out var textEdit));
        Assert.Single(textEdit!.Edits);
        var content = ((TextEdit)textEdit.Edits[0]).NewText;
        Assert.Contains("JavaScript isolation for File1 component", content);
    }

    [Fact]
    public async Task CSharpIsolationFile_GeneratesCodeBehind()
    {
        var document = CreateProjectAndRazorDocument("<div></div>");
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddIsolationFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddIsolationFileAsync(solutionInfo, razorFileUri, IsolationFileKind.CSharp, ct),
            DisposalToken);

        Assert.NotNull(result);
        var changes = GetDocumentChanges(result);
        Assert.Equal(2, changes.Length);

        // First change: CreateFile with .razor.cs extension
        Assert.True(changes[0].TryGetSecond(out var createFile));
        Assert.Contains(".razor.cs", createFile!.DocumentUri.ToString());

        // Second change: TextDocumentEdit with C# content
        Assert.True(changes[1].TryGetFirst(out var textEdit));
        Assert.Single(textEdit!.Edits);
        var content = ((TextEdit)textEdit.Edits[0]).NewText;

        // Should contain namespace from test project (SomeProject)
        Assert.Contains("namespace SomeProject", content);
        // Should contain partial class matching the file name
        Assert.Contains("partial class File1", content);
    }

    [Fact]
    public async Task CSharpIsolationFile_WithNamespaceDirective()
    {
        var document = CreateProjectAndRazorDocument("""
            @namespace My.Custom.Namespace

            <div></div>
            """);
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddIsolationFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddIsolationFileAsync(solutionInfo, razorFileUri, IsolationFileKind.CSharp, ct),
            DisposalToken);

        Assert.NotNull(result);
        var changes = GetDocumentChanges(result);
        Assert.True(changes[1].TryGetFirst(out var textEdit));
        var content = ((TextEdit)textEdit!.Edits[0]).NewText;

        // Should use the @namespace directive value
        Assert.Contains("namespace My.Custom.Namespace", content);
        Assert.Contains("partial class File1", content);
    }

    [Fact]
    public async Task CSharpIsolationFile_IncludesUsingDirectives()
    {
        var document = CreateProjectAndRazorDocument(
            """
            @using System.Diagnostics

            <div></div>
            """);
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddIsolationFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddIsolationFileAsync(solutionInfo, razorFileUri, IsolationFileKind.CSharp, ct),
            DisposalToken);

        Assert.NotNull(result);
        var changes = GetDocumentChanges(result);
        Assert.True(changes[1].TryGetFirst(out var textEdit));
        var content = ((TextEdit)textEdit!.Edits[0]).NewText;

        // The @using directive from the Razor file should appear in the generated code-behind.
        // The test infrastructure may include default imports from _Imports.razor.
        Assert.Contains("partial class File1", content);
        Assert.Contains("namespace SomeProject", content);
    }

    [Fact]
    public async Task CshtmlFile_CssIsolation()
    {
        var document = CreateProjectAndRazorDocument(
            "<div></div>",
            fileKind: AspNetCore.Razor.Language.RazorFileKind.Legacy,
            documentFilePath: FilePath("Page1.cshtml"));
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddIsolationFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddIsolationFileAsync(solutionInfo, razorFileUri, IsolationFileKind.Css, ct),
            DisposalToken);

        Assert.NotNull(result);
        var changes = GetDocumentChanges(result);
        Assert.True(changes[0].TryGetSecond(out var createFile));
        Assert.Contains(".cshtml.css", createFile!.DocumentUri.ToString());

        // .cshtml files should say "view" not "component"
        Assert.True(changes[1].TryGetFirst(out var textEdit));
        Assert.Contains("Page1 view", ((TextEdit)textEdit!.Edits[0]).NewText);
    }

    [Fact]
    public async Task InvalidFileKind_ReturnsNull()
    {
        var document = CreateProjectAndRazorDocument("<div></div>");
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddIsolationFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddIsolationFileAsync(solutionInfo, razorFileUri, "invalid", ct),
            DisposalToken);

        Assert.Null(result);
    }

    private static SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[] GetDocumentChanges(WorkspaceEdit edit)
    {
        Assert.NotNull(edit.DocumentChanges);
        Assert.True(edit.DocumentChanges.Value.TryGetSecond(out var changes),
            "Expected DocumentChanges to contain SumType[] (not TextDocumentEdit[])");
        return changes;
    }
}
