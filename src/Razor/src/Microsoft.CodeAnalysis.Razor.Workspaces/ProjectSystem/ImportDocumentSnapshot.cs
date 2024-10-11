﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal sealed class ImportDocumentSnapshot(IProjectSnapshot project, RazorProjectItem item) : IDocumentSnapshot
{
    private static readonly Task<VersionStamp> s_versionTask = Task.FromResult(VersionStamp.Default);

    public IProjectSnapshot Project { get; } = project;

    private readonly RazorProjectItem _importItem = item;
    private SourceText? _sourceText;

    // The default import file does not have a kind or paths.
    public string? FileKind => null;
    public string? FilePath => null;
    public string? TargetPath => null;

    public int Version => 1;

    public Task<SourceText> GetTextAsync()
    {
        return _sourceText is SourceText sourceText
            ? Task.FromResult(sourceText)
            : GetTextCoreAsync();

        Task<SourceText> GetTextCoreAsync()
        {
            using var stream = _importItem.Read();
            var sourceText = SourceText.From(stream);

            var result = _sourceText ??= InterlockedOperations.Initialize(ref _sourceText, sourceText);
            return Task.FromResult(result);
        }
    }

    public Task<RazorCodeDocument> GetGeneratedOutputAsync(bool forceDesignTimeGeneratedOutput)
        => throw new NotSupportedException();

    public Task<VersionStamp> GetTextVersionAsync()
        => s_versionTask;

    public bool TryGetText([NotNullWhen(true)] out SourceText? result)
    {
        result = _sourceText;
        return result is not null;
    }

    public bool TryGetTextVersion(out VersionStamp result)
    {
        result = VersionStamp.Default;
        return true;
    }

    public bool TryGetGeneratedOutput([NotNullWhen(true)] out RazorCodeDocument? result)
        => throw new NotSupportedException();

    public IDocumentSnapshot WithText(SourceText text)
        => throw new NotSupportedException();

    public Task<SyntaxTree> GetCSharpSyntaxTreeAsync(CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
