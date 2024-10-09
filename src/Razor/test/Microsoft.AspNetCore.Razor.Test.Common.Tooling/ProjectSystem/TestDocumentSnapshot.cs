// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;

internal sealed class TestDocumentSnapshot : IDocumentSnapshot
{
    public DocumentSnapshot RealSnapshot { get; }

    private readonly RazorCodeDocument? _codeDocument;

    private TestDocumentSnapshot(TestProjectSnapshot project, DocumentState state, RazorCodeDocument? codeDocument = null)
    {
        RealSnapshot = new DocumentSnapshot(project.RealSnapshot, state);
        _codeDocument = codeDocument;
    }

    public static TestDocumentSnapshot Create(string filePath)
        => Create(filePath, text: string.Empty, ProjectWorkspaceState.Default, version: 0);

    public static TestDocumentSnapshot Create(string filePath, string text, int version = 0)
        => Create(filePath, text, ProjectWorkspaceState.Default, version);

    public static TestDocumentSnapshot Create(string filePath, string text, ProjectWorkspaceState projectWorkspaceState, int version = 0)
    {
        var project = TestProjectSnapshot.Create(filePath + ".csproj", projectWorkspaceState);
        var hostDocument = TestHostDocument.Create(project.HostProject, filePath);

        var sourceText = SourceText.From(text);
        var textVersion = VersionStamp.Default;

        var documentState = new DocumentState(
            hostDocument,
            sourceText,
            textVersion,
            version,
            () => Task.FromResult(TextAndVersion.Create(sourceText, textVersion)));

        return new TestDocumentSnapshot(project, documentState);
    }

    public static TestDocumentSnapshot Create(string filePath, RazorCodeDocument codeDocument, int version = 0)
        => Create(filePath, codeDocument, ProjectWorkspaceState.Default, version);

    public static TestDocumentSnapshot Create(string filePath, RazorCodeDocument codeDocument, ProjectWorkspaceState projectWorkspaceState, int version = 0)
    {
        var project = TestProjectSnapshot.Create(filePath + ".csproj", projectWorkspaceState);
        var hostDocument = TestHostDocument.Create(project.HostProject, filePath);

        var sourceText = codeDocument.Source.Text;
        var textVersion = VersionStamp.Default;

        var documentState = new DocumentState(
            hostDocument,
            sourceText,
            textVersion,
            version,
            () => Task.FromResult(TextAndVersion.Create(sourceText, textVersion)));

        return new TestDocumentSnapshot(project, documentState, codeDocument);
    }

    public HostDocument HostDocument => RealSnapshot.HostDocument;

    public string? FileKind => RealSnapshot.FileKind;
    public string? FilePath => RealSnapshot.FilePath;
    public string? TargetPath => RealSnapshot.TargetPath;
    public IProjectSnapshot Project => RealSnapshot.Project;
    public bool SupportsOutput => RealSnapshot.SupportsOutput;
    public int Version => RealSnapshot.Version;

    public Task<RazorCodeDocument> GetGeneratedOutputAsync(bool forceDesignTimeGeneratedOutput)
    {
        return _codeDocument is null
            ? RealSnapshot.GetGeneratedOutputAsync(forceDesignTimeGeneratedOutput)
            : Task.FromResult(_codeDocument);
    }

    public Task<SourceText> GetTextAsync()
    {
        return _codeDocument is null
            ? RealSnapshot.GetTextAsync()
            : Task.FromResult(_codeDocument.Source.Text);
    }

    public Task<VersionStamp> GetTextVersionAsync()
        => RealSnapshot.GetTextVersionAsync();

    public Task<SyntaxTree> GetCSharpSyntaxTreeAsync(CancellationToken cancellationToken)
    {
        if (_codeDocument is { } codeDocument)
        {
            var csharpText = codeDocument.GetCSharpSourceText();
            var csharpSyntaxTree = CSharpSyntaxTree.ParseText(csharpText, cancellationToken: cancellationToken);

            return Task.FromResult(csharpSyntaxTree);
        }

        return RealSnapshot.GetCSharpSyntaxTreeAsync(cancellationToken);
    }

    public bool TryGetGeneratedOutput([NotNullWhen(true)] out RazorCodeDocument? result)
    {
        if (_codeDocument is { } codeDocument)
        {
            result = codeDocument;
            return true;
        }

        return RealSnapshot.TryGetGeneratedOutput(out result);
    }

    public bool TryGetText([NotNullWhen(true)] out SourceText? result)
    {
        if (_codeDocument is { } codeDocument)
        {
            result = codeDocument.Source.Text;
            return true;
        }

        return RealSnapshot.TryGetText(out result);
    }

    public bool TryGetTextVersion(out VersionStamp result)
        => RealSnapshot.TryGetTextVersion(out result);

    public IDocumentSnapshot WithText(SourceText text)
        => RealSnapshot.WithText(text);
}
