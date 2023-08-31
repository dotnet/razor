// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal sealed class RazorProjectInfoFormatter : MessagePackFormatter<RazorProjectInfo>
{
    public static readonly MessagePackFormatter<RazorProjectInfo> Instance = new RazorProjectInfoFormatter();

    private RazorProjectInfoFormatter()
    {
    }

    public override RazorProjectInfo Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        reader.ReadArrayHeaderAndVerify(7);

        var version = reader.ReadInt32();

        if (version != SerializationFormat.Version)
        {
            throw new RazorProjectInfoSerializationException(SR.Unsupported_razor_project_info_version_encountered);
        }

        var serializedFilePath = DeserializeString(ref reader, options);
        var filePath = DeserializeString(ref reader, options);
        var configuration = reader.DeserializeObjectOrNull<RazorConfiguration>(options);
        var projectWorkspaceState = reader.DeserializeObjectOrNull<ProjectWorkspaceState>(options);
        var rootNamespace = DeserializeStringOrNull(ref reader, options);
        var documents = reader.DeserializeObject<ImmutableArray<DocumentSnapshotHandle>>(options);

        return new RazorProjectInfo(serializedFilePath, filePath, configuration, rootNamespace, projectWorkspaceState, documents);
    }

    public override void Serialize(ref MessagePackWriter writer, RazorProjectInfo value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(7);

        writer.Write(SerializationFormat.Version);
        writer.Write(value.SerializedFilePath);
        writer.Write(value.FilePath);
        writer.SerializeObject(value.Configuration, options);
        writer.SerializeObject(value.ProjectWorkspaceState, options);
        writer.Write(value.RootNamespace);
        writer.SerializeObject(value.Documents, options);
    }
}
