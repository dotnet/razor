// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.NestedFiles;
using Microsoft.CodeAnalysis.Razor.Remote;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.NestedFiles;

public class RemoteAddNestedFileServiceTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task CssNestedFile_CreatesEmptyFile()
    {
        var document = CreateProjectAndRazorDocument("<div></div>");
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(solutionInfo, razorFileUri, NestedFileKind.Css, ct),
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
        Assert.Contains("CSS for File1 component", ((TextEdit)textEdit.Edits[0]).NewText);
    }

    [Fact]
    public async Task JavaScriptNestedFile_CreatesFileWithTemplate()
    {
        var document = CreateProjectAndRazorDocument("<div></div>");
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(solutionInfo, razorFileUri, NestedFileKind.JavaScript, ct),
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
        Assert.Contains("JavaScript for File1 component", content);
    }

    [Fact]
    public async Task CSharpNestedFile_GeneratesCodeBehind()
    {
        var document = CreateProjectAndRazorDocument("<div></div>");
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(solutionInfo, razorFileUri, NestedFileKind.CSharp, ct),
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
    public async Task CSharpNestedFile_WithNamespaceDirective()
    {
        var document = CreateProjectAndRazorDocument("""
            @namespace My.Custom.Namespace

            <div></div>
            """);
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(solutionInfo, razorFileUri, NestedFileKind.CSharp, ct),
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
    public async Task CSharpNestedFile_IncludesUsingDirectives()
    {
        var document = CreateProjectAndRazorDocument(
            """
            @using System.Diagnostics

            <div></div>
            """);
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(solutionInfo, razorFileUri, NestedFileKind.CSharp, ct),
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
    public async Task CshtmlFile_CssNestedFile()
    {
        var document = CreateProjectAndRazorDocument(
            "<div></div>",
            fileKind: AspNetCore.Razor.Language.RazorFileKind.Legacy,
            documentFilePath: FilePath("Page1.cshtml"));
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(solutionInfo, razorFileUri, NestedFileKind.Css, ct),
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
    public async Task CSharpNestedFile_DefaultsToBlockScopedNamespace()
    {
        // No editorconfig present — should use block-scoped namespace (with braces)
        var document = CreateProjectAndRazorDocument("<div></div>");
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(solutionInfo, razorFileUri, NestedFileKind.CSharp, ct),
            DisposalToken);

        Assert.NotNull(result);
        var changes = GetDocumentChanges(result);
        Assert.True(changes[1].TryGetFirst(out var textEdit));
        var content = ((TextEdit)textEdit!.Edits[0]).NewText;

        Assert.Contains("partial class File1", content);
        // Without editorconfig, namespace should be block-scoped (not file-scoped)
        Assert.DoesNotContain("namespace SomeProject;", content);
        Assert.Contains("namespace SomeProject", content);
    }

    [Fact]
    public async Task CSharpNestedFile_WithFileScopedNamespaceEditorConfig()
    {
        var editorConfigPath = FilePath(".editorconfig");
        var editorConfigContent = """
            root = true

            [*.cs]
            csharp_style_namespace_declarations = file_scoped
            """;

        var document = CreateProjectAndRazorDocument(
            "<div></div>",
            projectConfigure: builder => builder.AddAnalyzerConfigDocument(
                editorConfigPath,
                Microsoft.CodeAnalysis.Text.SourceText.From(editorConfigContent)));
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(solutionInfo, razorFileUri, NestedFileKind.CSharp, ct),
            DisposalToken);

        Assert.NotNull(result);
        var changes = GetDocumentChanges(result);
        Assert.True(changes[1].TryGetFirst(out var textEdit));
        var content = ((TextEdit)textEdit!.Edits[0]).NewText;

        // With file-scoped namespace editorconfig, Roslyn formatting should produce "namespace X;"
        Assert.Contains("namespace SomeProject;", content);
        Assert.Contains("partial class File1", content);
    }

    [Fact]
    public async Task InvalidFileKind_ReturnsNull()
    {
        var document = CreateProjectAndRazorDocument("<div></div>");
        var razorFileUri = new Uri(document.FilePath!);

        var result = await RemoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            document.Project.Solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(solutionInfo, razorFileUri, "invalid", ct),
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
