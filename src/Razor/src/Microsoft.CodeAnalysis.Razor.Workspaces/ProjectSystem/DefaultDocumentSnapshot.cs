// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class DefaultDocumentSnapshot : DocumentSnapshot
{
    public override string FileKind => State.HostDocument.FileKind;
    public override string FilePath => State.HostDocument.FilePath;
    public override string TargetPath => State.HostDocument.TargetPath;
    public override ProjectSnapshot Project => ProjectInternal;
    public override bool SupportsOutput => true;

    public DefaultProjectSnapshot ProjectInternal { get; }
    public DocumentState State { get; }

    public DefaultDocumentSnapshot(DefaultProjectSnapshot project, DocumentState state)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ProjectInternal = project;
        State = state;
    }

    public override ImmutableArray<DocumentSnapshot> GetImports()
        => State.GetImports(ProjectInternal);

    public override Task<SourceText> GetTextAsync()
        => State.GetTextAsync();

    public override Task<VersionStamp> GetTextVersionAsync()
        => State.GetTextVersionAsync();

    public override async Task<RazorCodeDocument> GetGeneratedOutputAsync()
    {
        var (output, _) = await State.GetGeneratedOutputAndVersionAsync(ProjectInternal, this).ConfigureAwait(false);
        return output;
    }

    public override bool TryGetText([NotNullWhen(true)] out SourceText? result)
        => State.TryGetText(out result);

    public override bool TryGetTextVersion(out VersionStamp result)
        => State.TryGetTextVersion(out result);

    public override bool TryGetGeneratedOutput([NotNullWhen(true)] out RazorCodeDocument? result)
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
}
