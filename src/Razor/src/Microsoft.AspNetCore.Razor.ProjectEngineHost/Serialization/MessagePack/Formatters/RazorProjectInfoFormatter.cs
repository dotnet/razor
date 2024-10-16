// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using MessagePack;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal sealed class RazorProjectInfoFormatter : TopLevelFormatter<RazorProjectInfo>
{
    public static readonly TopLevelFormatter<RazorProjectInfo> Instance = new RazorProjectInfoFormatter();

    private RazorProjectInfoFormatter()
    {
    }

    public override RazorProjectInfo Deserialize(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        VerifyVersionNumber(in reader);

        reader.ReadArrayHeaderAndVerify(4);

        // We skip the version number here, since we verified it above.
        reader.Skip();

        var hostProject = reader.Deserialize<HostProject>(options);
        var projectWorkspaceState = reader.DeserializeOrNull<ProjectWorkspaceState>(options) ?? ProjectWorkspaceState.Default;
        var documents = reader.Deserialize<ImmutableArray<HostDocument>>(options);

        return new RazorProjectInfo(hostProject, projectWorkspaceState, documents);
    }

    public override void Serialize(ref MessagePackWriter writer, RazorProjectInfo value, SerializerCachingOptions options)
    {
        writer.WriteArrayHeader(4);

        writer.Write(SerializationFormat.Version);
        writer.Serialize(value.HostProject, options);
        writer.Serialize(value.ProjectWorkspaceState, options);
        writer.Serialize(value.Documents, options);
    }

    private static void VerifyVersionNumber(ref readonly MessagePackReader reader)
    {
        // Peek ahead to check version number.
        var peekReader = reader.CreatePeekReader();

        if (peekReader.TryReadArrayHeader(out var count) && count > 0)
        {
            var version = peekReader.ReadInt32();

            if (version != SerializationFormat.Version)
            {
                throw new RazorProjectInfoSerializationException(SR.Unsupported_razor_project_info_version_encountered);
            }
        }
    }
}
