// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    private record struct BoundAttributeReader(BoundAttributeDescriptorBuilder Builder)
    {
        public static readonly PropertyMap<BoundAttributeReader> PropertyMap = new(
            new(nameof(BoundAttributeDescriptor.Kind), ReadKind),
            new(nameof(BoundAttributeDescriptor.Name), ReadName),
            new(nameof(BoundAttributeDescriptor.TypeName), ReadTypeName),
            new(nameof(BoundAttributeDescriptor.Documentation), ReadDocumentation),
            new(nameof(BoundAttributeDescriptor.IndexerNamePrefix), ReadIndexerNamePrefix),
            new(nameof(BoundAttributeDescriptor.IndexerTypeName), ReadIndexerTypeName),
            new(nameof(BoundAttributeDescriptor.IsEnum), ReadIsEnum),
            new(nameof(BoundAttributeDescriptor.IsEditorRequired), ReadIsEditorRequired),
            new(nameof(BoundAttributeDescriptor.BoundAttributeParameters), ReadBoundAttributeParameters),
            new(nameof(BoundAttributeDescriptor.Metadata), ReadMetadata),
            new(nameof(BoundAttributeDescriptor.Diagnostics), ReadDiagnostics));

        public static void ReadKind(JsonReader reader, ref BoundAttributeReader arg)
        {
            // In old serialized files, Kind might appear, though it isn't meaningful.
            _ = reader.ReadString();
        }

        public static void ReadName(JsonReader reader, ref BoundAttributeReader arg)
            => arg.Builder.Name = Cached(reader.ReadString());

        public static void ReadTypeName(JsonReader reader, ref BoundAttributeReader arg)
            => arg.Builder.TypeName = Cached(reader.ReadString());

        public static void ReadDocumentation(JsonReader reader, ref BoundAttributeReader arg)
                => arg.Builder.Documentation = Cached(reader.ReadString());

        public static void ReadIndexerNamePrefix(JsonReader reader, ref BoundAttributeReader arg)
        {
            if (reader.ReadString() is { } indexerNamePrefix)
            {
                var builder = arg.Builder;
                builder.IsDictionary = true;
                builder.IndexerAttributeNamePrefix = Cached(indexerNamePrefix);
            }
        }

        public static void ReadIndexerTypeName(JsonReader reader, ref BoundAttributeReader arg)
        {
            if (reader.ReadString() is { } indexerTypeName)
            {
                var builder = arg.Builder;
                builder.IsDictionary = true;
                builder.IndexerValueTypeName = Cached(indexerTypeName);
            }
        }

        public static void ReadIsEnum(JsonReader reader, ref BoundAttributeReader arg)
            => arg.Builder.IsEnum = reader.ReadBoolean();

        public static void ReadIsEditorRequired(JsonReader reader, ref BoundAttributeReader arg)
            => arg.Builder.IsEditorRequired = reader.ReadBoolean();

        public static void ReadBoundAttributeParameters(JsonReader reader, ref BoundAttributeReader arg)
        {
            reader.ProcessArray(arg.Builder, static (reader, builder) =>
            {
                builder.BindAttributeParameter(parameterBuilder =>
                {
                    reader.ProcessObject(new BoundAttributeParameterReader(parameterBuilder), BoundAttributeParameterReader.PropertyMap);
                });
            });
        }

        public static void ReadMetadata(JsonReader reader, ref BoundAttributeReader arg)
            => reader.ProcessObject(arg.Builder.Metadata, ProcessMetadata);

        public static void ReadDiagnostics(JsonReader reader, ref BoundAttributeReader arg)
            => reader.ProcessArray(arg.Builder.Diagnostics, ProcessDiagnostic);
    }
}
