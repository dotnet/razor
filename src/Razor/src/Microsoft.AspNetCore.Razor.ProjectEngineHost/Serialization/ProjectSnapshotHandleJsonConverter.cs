// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal class ProjectSnapshotHandleJsonConverter : ObjectJsonConverter<ProjectSnapshotHandle>
{
    public static readonly ProjectSnapshotHandleJsonConverter Instance = new();

    protected override ProjectSnapshotHandle ReadFromProperties(JsonReader reader)
    {
        Data data = default;
        reader.ReadProperties(ref data, Data.PropertyMap);

        return new(data.FilePath, data.Configuration, data.RootNamespace);
    }

    private record struct Data(string FilePath, RazorConfiguration? Configuration, string? RootNamespace)
    {
        public static readonly PropertyMap<Data> PropertyMap = new(
            (nameof(Data.FilePath), ReadFilePath),
            (nameof(Data.Configuration), ReadConfiguration),
            (nameof(Data.RootNamespace), ReadRootNamespace));

        public static void ReadFilePath(JsonReader reader, ref Data data)
            => data.FilePath = reader.ReadNonNullString();

        public static void ReadConfiguration(JsonReader reader, ref Data data)
            => data.Configuration = reader.ReadObject(ObjectReaders.ReadConfigurationFromProperties);

        public static void ReadRootNamespace(JsonReader reader, ref Data data)
            => data.RootNamespace = reader.ReadString();
    }

    protected override void WriteProperties(JsonWriter writer, ProjectSnapshotHandle value)
    {
        writer.Write(nameof(value.FilePath), value.FilePath);
        writer.WriteObject(nameof(value.Configuration), value.Configuration, ObjectWriters.WriteProperties);
        writer.Write(nameof(value.RootNamespace), value.RootNamespace);
    }
}
