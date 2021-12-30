// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Serialization
{
    internal class FullProjectSnapshotHandleJsonConverter : JsonConverter
    {
        public static readonly FullProjectSnapshotHandleJsonConverter Instance = new FullProjectSnapshotHandleJsonConverter();
        private const string SerializationFormatPropertyName = "SerializationFormat";

        public override bool CanConvert(Type objectType)
        {
            return typeof(FullProjectSnapshotHandle).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                return null;
            }

            var (_, _, _, _, serializationFormat, filePath, configuration, rootNamespace, projectWorkspaceState, documents) = reader.ReadProperties(static (propertyName, arg) =>
            {
                var (reader, objectType, existingValue, serializer, serializationFormat, filePath, configuration, rootNamespace, projectWorkspaceState, documents) = (arg.reader, arg.objectType, arg.existingValue, arg.serializer, arg.serializationFormat, arg.filePath, arg.configuration, arg.rootNamespace, arg.projectWorkspaceState, arg.documents);
                switch (propertyName)
                {
                    case SerializationFormatPropertyName:
                        if (reader.Read())
                        {
                            serializationFormat = (string)reader.Value;
                        }

                        break;
                    case nameof(FullProjectSnapshotHandle.FilePath):
                        if (reader.Read())
                        {
                            filePath = (string)reader.Value;
                        }

                        break;
                    case nameof(FullProjectSnapshotHandle.Configuration):
                        if (reader.Read())
                        {
                            configuration = RazorConfigurationJsonConverter.Instance.ReadJson(reader, objectType, existingValue, serializer) as RazorConfiguration;
                        }

                        break;
                    case nameof(FullProjectSnapshotHandle.RootNamespace):
                        if (reader.Read())
                        {
                            rootNamespace = (string)reader.Value;
                        }

                        break;
                    case nameof(FullProjectSnapshotHandle.ProjectWorkspaceState):
                        if (reader.Read())
                        {
                            projectWorkspaceState = serializer.Deserialize<ProjectWorkspaceState>(reader);
                        }

                        break;
                    case nameof(FullProjectSnapshotHandle.Documents):
                        if (reader.Read())
                        {
                            documents = serializer.Deserialize<DocumentSnapshotHandle[]>(reader);
                        }

                        break;
                }

                return (reader, objectType, existingValue, serializer, serializationFormat, filePath, configuration, rootNamespace, projectWorkspaceState, documents);
            }, (reader, objectType, existingValue, serializer, serializationFormat: (string)null, filePath: (string)null, configuration: (RazorConfiguration)null, rootNamespace: (string)null, projectWorkspaceState: (ProjectWorkspaceState)null, documents: (DocumentSnapshotHandle[])null));

            // We need to add a serialization format to the project response to indicate that this version of the code is compatible with what's being serialized.
            // This scenario typically happens when a user has an incompatible serialized project snapshot but is using the latest Razor bits.

            if (string.IsNullOrEmpty(serializationFormat) || serializationFormat != ProjectSerializationFormat.Version)
            {
                // Unknown serialization format.
                return null;
            }

            return new FullProjectSnapshotHandle(filePath, configuration, rootNamespace, projectWorkspaceState, documents);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var handle = (FullProjectSnapshotHandle)value;

            writer.WriteStartObject();

            writer.WritePropertyName(nameof(FullProjectSnapshotHandle.FilePath));
            writer.WriteValue(handle.FilePath);

            if (handle.Configuration is null)
            {
                writer.WritePropertyName(nameof(FullProjectSnapshotHandle.Configuration));
                writer.WriteNull();
            }
            else
            {
                writer.WritePropertyName(nameof(FullProjectSnapshotHandle.Configuration));
                serializer.Serialize(writer, handle.Configuration);
            }

            if (handle.ProjectWorkspaceState is null)
            {
                writer.WritePropertyName(nameof(FullProjectSnapshotHandle.ProjectWorkspaceState));
                writer.WriteNull();
            }
            else
            {
                writer.WritePropertyName(nameof(FullProjectSnapshotHandle.ProjectWorkspaceState));
                serializer.Serialize(writer, handle.ProjectWorkspaceState);
            }

            writer.WritePropertyName(nameof(FullProjectSnapshotHandle.RootNamespace));
            writer.WriteValue(handle.RootNamespace);

            writer.WritePropertyName(nameof(FullProjectSnapshotHandle.Documents));
            serializer.Serialize(writer, handle.Documents);

            writer.WritePropertyName(SerializationFormatPropertyName);
            writer.WriteValue(ProjectSerializationFormat.Version);

            writer.WriteEndObject();
        }
    }
}
