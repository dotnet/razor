// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Resolvers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.ProjectSystem;

internal sealed record class RazorProjectInfo
{
    private static readonly MessagePackSerializerOptions s_options = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(
            RazorProjectInfoResolver.Instance,
            StandardResolver.Instance));

    public HostProject HostProject { get; }

    public ProjectKey Key => HostProject.Key;
    public string FilePath => HostProject.FilePath;
    public RazorConfiguration Configuration => HostProject.Configuration;
    public string? RootNamespace => HostProject.RootNamespace;
    public string DisplayName => HostProject.DisplayName;

    public ProjectWorkspaceState ProjectWorkspaceState { get; }
    public ImmutableArray<HostDocument> Documents { get; }

    public RazorProjectInfo(
        HostProject hostProject,
        ProjectWorkspaceState projectWorkspaceState,
        ImmutableArray<HostDocument> documents)
    {
        HostProject = hostProject;
        ProjectWorkspaceState = projectWorkspaceState;
        Documents = documents.NullToEmpty();
    }

    public bool Equals(RazorProjectInfo? other)
        => other is not null &&
           HostProject == other.HostProject &&
           ProjectWorkspaceState.Equals(other.ProjectWorkspaceState) &&
           Documents.SequenceEqual(other.Documents);

    public override int GetHashCode()
    {
        var hash = HashCodeCombiner.Start();

        hash.Add(HostProject);
        hash.Add(ProjectWorkspaceState);
        hash.Add(Documents);

        return hash.CombinedHash;
    }

    public byte[] Serialize()
        => MessagePackSerializer.Serialize(this, s_options);

    public void SerializeTo(IBufferWriter<byte> bufferWriter)
        => MessagePackSerializer.Serialize(bufferWriter, this, s_options);

    public void SerializeTo(Stream stream)
        => MessagePackSerializer.Serialize(stream, this, s_options);

    public static RazorProjectInfo? DeserializeFrom(ReadOnlyMemory<byte> buffer)
        => MessagePackSerializer.Deserialize<RazorProjectInfo>(buffer, s_options);

    public static RazorProjectInfo? DeserializeFrom(Stream stream)
        => MessagePackSerializer.Deserialize<RazorProjectInfo>(stream, s_options);

    public static ValueTask<RazorProjectInfo> DeserializeFromAsync(Stream stream, CancellationToken cancellationToken)
        => MessagePackSerializer.DeserializeAsync<RazorProjectInfo>(stream, s_options, cancellationToken);
}
