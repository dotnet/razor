// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.ProjectSystem;

internal sealed record class RazorProjectInfo
{
    public ProjectKey ProjectKey { get; init; }
    public string FilePath { get; init; }
    public RazorConfiguration Configuration { get; init; }
    public string? RootNamespace { get; init; }
    public string DisplayName { get; init; }
    public ProjectWorkspaceState ProjectWorkspaceState { get; init; }
    public ImmutableArray<DocumentSnapshotHandle> Documents { get; init; }

    private Checksum? _checksum;
    internal Checksum Checksum
        => _checksum ?? InterlockedOperations.Initialize(ref _checksum, ComputeChecksum());

    public RazorProjectInfo(
        ProjectKey projectKey,
        string filePath,
        RazorConfiguration configuration,
        string? rootNamespace,
        string displayName,
        ProjectWorkspaceState projectWorkspaceState,
        ImmutableArray<DocumentSnapshotHandle> documents)
    {
        ProjectKey = projectKey;
        FilePath = filePath;
        Configuration = configuration;
        RootNamespace = rootNamespace;
        DisplayName = displayName;
        ProjectWorkspaceState = projectWorkspaceState;
        Documents = documents.NullToEmpty();
    }

    public bool Equals(RazorProjectInfo? other)
        => other is not null &&
           ProjectKey == other.ProjectKey &&
           FilePath == other.FilePath &&
           Configuration.Equals(other.Configuration) &&
           RootNamespace == other.RootNamespace &&
           DisplayName == other.DisplayName &&
           ProjectWorkspaceState.Equals(other.ProjectWorkspaceState) &&
           Documents.SequenceEqual(other.Documents);

    public override int GetHashCode()
    {
        var hash = HashCodeCombiner.Start();

        hash.Add(ProjectKey);
        hash.Add(FilePath);
        hash.Add(Configuration);
        hash.Add(RootNamespace);
        hash.Add(DisplayName);
        hash.Add(ProjectWorkspaceState);
        hash.Add(Documents);

        return hash.CombinedHash;
    }

    private Checksum ComputeChecksum()
    {
        var builder = new Checksum.Builder();

        builder.AppendData(FilePath);
        builder.AppendData(ProjectKey.Id);
        builder.AppendData(DisplayName);

        Configuration.CalculateChecksum(builder);
        foreach (var document in Documents)
        {
            document.CalculateChecksum(builder);
        }

        ProjectWorkspaceState.CalculateChecksum(builder);

        return builder.FreeAndGetChecksum();
    }
}
