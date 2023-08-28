// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal sealed class ProjectRazorJsonFormatter : MessagePackFormatter<ProjectRazorJson>
{
    public static readonly MessagePackFormatter<ProjectRazorJson> Instance = new ProjectRazorJsonFormatter();

    private ProjectRazorJsonFormatter()
    {
    }

    public override ProjectRazorJson Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.NextMessagePackType != MessagePackType.Integer)
        {
            throw new ProjectRazorJsonSerializationException(SR.Unsupported_project_razor_json_version_encountered);
        }

        var version = reader.ReadInt32();

        if (version != ProjectRazorJson.Version)
        {
            throw new ProjectRazorJsonSerializationException(SR.Unsupported_project_razor_json_version_encountered);
        }

        var serializedFilePath = DeserializeString(ref reader, options);
        var filePath = DeserializeString(ref reader, options);
        var configuration = RazorConfigurationFormatter.Instance.AllowNull.Deserialize(ref reader, options);
        var projectWorkspaceState = ProjectWorkspaceStateFormatter.Instance.AllowNull.Deserialize(ref reader, options);
        var rootNamespace = AllowNull.DeserializeString(ref reader, options);
        var documents = DocumentSnapshotHandleFormatter.Instance.DeserializeImmutableArray(ref reader, options);

        return new ProjectRazorJson(serializedFilePath, filePath, configuration, rootNamespace, projectWorkspaceState, documents);
    }

    public override void Serialize(ref MessagePackWriter writer, ProjectRazorJson value, MessagePackSerializerOptions options)
    {
        writer.Write(ProjectRazorJson.Version);
        writer.Write(value.SerializedFilePath);
        writer.Write(value.FilePath);
        RazorConfigurationFormatter.Instance.AllowNull.Serialize(ref writer, value.Configuration, options);
        ProjectWorkspaceStateFormatter.Instance.AllowNull.Serialize(ref writer, value.ProjectWorkspaceState, options);
        writer.Write(value.RootNamespace);
        DocumentSnapshotHandleFormatter.Instance.SerializeArray(ref writer, value.Documents, options);
    }
}
