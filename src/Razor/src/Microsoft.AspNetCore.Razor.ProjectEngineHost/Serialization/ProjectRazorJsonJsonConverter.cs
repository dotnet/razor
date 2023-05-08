// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal class ProjectRazorJsonJsonConverter : ObjectJsonConverter<ProjectRazorJson>
{
    public static readonly ProjectRazorJsonJsonConverter Instance = new();

    protected override ProjectRazorJson ReadFromProperties(JsonDataReader reader)
    {
        Data data = default;
        reader.ReadProperties(ref data, Data.PropertyMap);

        // We need to add a serialization format to the project response to indicate that this version
        // of the code is compatible with what's being serialized. This scenario typically happens when
        // a user has an incompatible serialized project snapshot but is using the latest Razor bits.

        if (string.IsNullOrEmpty(data.SerializationFormat) || data.SerializationFormat != ProjectSerializationFormat.Version)
        {
            // Unknown serialization format.
            return null!;
        }

        return new ProjectRazorJson(
            data.SerializedFilePath, data.FilePath, data.Configuration, data.RootNamespace, data.ProjectWorkspaceState, data.Documents);
    }

    private record struct Data(
        string SerializedFilePath,
        string FilePath,
        RazorConfiguration? Configuration,
        string? RootNamespace,
        ProjectWorkspaceState? ProjectWorkspaceState,
        DocumentSnapshotHandle[] Documents,
        string? SerializationFormat)
    {
        public static readonly PropertyMap<Data> PropertyMap = new(
            (nameof(SerializedFilePath), ReadSerializedFilePath),
            (nameof(FilePath), ReadFilePath),
            (nameof(Configuration), ReadConfiguration),
            (nameof(RootNamespace), ReadRootNamespace),
            (nameof(ProjectWorkspaceState), ReadProjectWorkspaceState),
            (nameof(Documents), ReadDocuments),
            (nameof(SerializationFormat), ReadSerializationFormat));

        private static void ReadSerializedFilePath(JsonDataReader reader, ref Data data)
            => data.SerializedFilePath = reader.ReadNonNullString();

        private static void ReadFilePath(JsonDataReader reader, ref Data data)
            => data.FilePath = reader.ReadNonNullString();

        private static void ReadConfiguration(JsonDataReader reader, ref Data data)
            => data.Configuration = reader.ReadObject(ObjectReaders.ReadConfigurationFromProperties);

        private static void ReadRootNamespace(JsonDataReader reader, ref Data data)
            => data.RootNamespace = reader.ReadString();

        private static void ReadProjectWorkspaceState(JsonDataReader reader, ref Data data)
            => data.ProjectWorkspaceState = reader.ReadObject(ObjectReaders.ReadProjectWorkspaceStateFromProperties);

        private static void ReadDocuments(JsonDataReader reader, ref Data data)
            => data.Documents = reader.ReadArrayOrEmpty(ObjectReaders.ReadDocumentSnapshotHandle);

        private static void ReadSerializationFormat(JsonDataReader reader, ref Data data)
            => data.SerializationFormat = reader.ReadString();
    }

    protected override void WriteProperties(JsonDataWriter writer, ProjectRazorJson value)
    {
        writer.Write(nameof(value.SerializedFilePath), value.SerializedFilePath);
        writer.Write(nameof(value.FilePath), value.FilePath);
        writer.WriteObject(nameof(value.Configuration), value.Configuration, ObjectWriters.WriteProperties);
        writer.WriteObject(nameof(value.ProjectWorkspaceState), value.ProjectWorkspaceState, ObjectWriters.WriteProperties);
        writer.Write(nameof(value.RootNamespace), value.RootNamespace);
        writer.WriteArray(nameof(value.Documents), value.Documents, ObjectWriters.Write);
        writer.Write("SerializationFormat", ProjectSerializationFormat.Version);
    }
}
