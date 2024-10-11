// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal sealed class DocumentSnapshot(ProjectSnapshot project, DocumentState state) : IDocumentSnapshot
{
    private readonly DocumentState _state = state;
    private readonly ProjectSnapshot _project = project;

    public HostDocument HostDocument => _state.HostDocument;

    public string FileKind => _state.HostDocument.FileKind;
    public string FilePath => _state.HostDocument.FilePath;
    public string TargetPath => _state.HostDocument.TargetPath;
    public IProjectSnapshot Project => _project;
    public int Version => _state.Version;

    public Task<SourceText> GetTextAsync()
        => _state.GetTextAsync();

    public Task<VersionStamp> GetTextVersionAsync()
        => _state.GetTextVersionAsync();

    public bool TryGetText([NotNullWhen(true)] out SourceText? result)
        => _state.TryGetText(out result);

    public bool TryGetTextVersion(out VersionStamp result)
        => _state.TryGetTextVersion(out result);

    public bool TryGetGeneratedOutput([NotNullWhen(true)] out RazorCodeDocument? result)
    {
        if (_state.IsGeneratedOutputResultAvailable)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            result = _state.GetGeneratedOutputAndVersionAsync(_project, this).Result.output;
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            return true;
        }

        result = null;
        return false;
    }

    public IDocumentSnapshot WithText(SourceText text)
    {
        return new DocumentSnapshot(_project, _state.WithText(text, VersionStamp.Create()));
    }

    public async Task<SyntaxTree> GetCSharpSyntaxTreeAsync(CancellationToken cancellationToken)
    {
        var codeDocument = await GetGeneratedOutputAsync(forceDesignTimeGeneratedOutput: false).ConfigureAwait(false);
        var csharpText = codeDocument.GetCSharpSourceText();
        return CSharpSyntaxTree.ParseText(csharpText, cancellationToken: cancellationToken);
    }

    public async Task<RazorCodeDocument> GetGeneratedOutputAsync(bool forceDesignTimeGeneratedOutput)
    {
        if (forceDesignTimeGeneratedOutput)
        {
            return await GetDesignTimeGeneratedOutputAsync().ConfigureAwait(false);
        }

        var (output, _) = await _state.GetGeneratedOutputAndVersionAsync(_project, this).ConfigureAwait(false);
        return output;
    }

    private async Task<RazorCodeDocument> GetDesignTimeGeneratedOutputAsync()
    {
        var tagHelpers = await Project.GetTagHelpersAsync(CancellationToken.None).ConfigureAwait(false);
        var projectEngine = Project.GetProjectEngine();
        var imports = await DocumentState.GetImportsAsync(this, projectEngine).ConfigureAwait(false);
        return await DocumentState.GenerateCodeDocumentAsync(this, projectEngine, imports, tagHelpers, forceRuntimeCodeGeneration: false).ConfigureAwait(false);
    }
}
