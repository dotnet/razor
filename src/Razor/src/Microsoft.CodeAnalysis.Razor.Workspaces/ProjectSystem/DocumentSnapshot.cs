// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class DocumentSnapshot : IDocumentSnapshot
{
    public string FileKind => State.HostDocument.FileKind;
    public string FilePath => State.HostDocument.FilePath;
    public string TargetPath => State.HostDocument.TargetPath;
    public IProjectSnapshot Project => ProjectInternal;
    public bool SupportsOutput => true;

    public ProjectSnapshot ProjectInternal { get; }
    public DocumentState State { get; }

    public DocumentSnapshot(ProjectSnapshot project, DocumentState state)
    {
        ProjectInternal = project ?? throw new ArgumentNullException(nameof(project));
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public Task<SourceText> GetTextAsync()
        => State.GetTextAsync();

    public Task<VersionStamp> GetTextVersionAsync()
        => State.GetTextVersionAsync();

    public virtual async Task<RazorCodeDocument> GetGeneratedOutputAsync()
    {
        var (output, _) = await State.GetGeneratedOutputAndVersionAsync(ProjectInternal, this).ConfigureAwait(false);
        return output;
    }

    public bool TryGetText([NotNullWhen(true)] out SourceText? result)
        => State.TryGetText(out result);

    public bool TryGetTextVersion(out VersionStamp result)
        => State.TryGetTextVersion(out result);

    public virtual bool TryGetGeneratedOutput([NotNullWhen(true)] out RazorCodeDocument? result)
    {
        if (State.IsGeneratedOutputResultAvailable)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            result = State.GetGeneratedOutputAndVersionAsync(ProjectInternal, this).Result.output;
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            return true;
        }

        result = null;
        return false;
    }

    public IDocumentSnapshot WithText(SourceText text)
    {
        return new DocumentSnapshot(ProjectInternal, State.WithText(text, VersionStamp.Create()));
    }

    public async Task<SyntaxTree> GetCSharpSyntaxTreeAsync(CancellationToken cancellationToken)
    {
        var codeDocument = await GetGeneratedOutputAsync().ConfigureAwait(false);
        var csharpText = codeDocument.GetCSharpSourceText();
        return CSharpSyntaxTree.ParseText(csharpText, cancellationToken: cancellationToken);
    }

    public Task<RazorCodeDocument> GetFormatterCodeDocumentAsync()
    {
        var forceRuntimeCodeGeneration = Project.Configuration.LanguageServerFlags?.ForceRuntimeCodeGeneration ?? false;
        if (!forceRuntimeCodeGeneration)
        {
            return GetGeneratedOutputAsync();
        }

        // if forceRuntimeCodeGeneration is on, GetGeneratedOutputAsync will get runtime code. As of now
        // the formatting service doesn't expect the form of code generated to be what the compiler does with
        // runtime. For now force usage of design time and avoid the cache. There may be a slight perf hit
        // but either the user is typing (which will invalidate the cache) or the user is manually attempting to
        // format. We expect formatting to invalidate the cache if it changes things and consider this an
        // acceptable overhead for now.
        return GetDesignTimeGeneratedOutputAsync();
    }

    private async Task<RazorCodeDocument> GetDesignTimeGeneratedOutputAsync()
    {
        var tagHelpers = await Project.GetTagHelpersAsync(CancellationToken.None).ConfigureAwait(false);
        var projectEngine = Project.GetProjectEngine();
        var imports = await DocumentState.GetImportsAsync(this, projectEngine).ConfigureAwait(false);
        return await DocumentState.GenerateCodeDocumentAsync(this, Project.GetProjectEngine(), imports, tagHelpers, false).ConfigureAwait(false);
    }
}
