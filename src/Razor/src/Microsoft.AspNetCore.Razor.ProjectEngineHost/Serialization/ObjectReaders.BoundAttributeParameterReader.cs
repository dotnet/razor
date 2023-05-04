// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    private record struct BoundAttributeParameterReader(BoundAttributeParameterDescriptorBuilder Builder)
    {
        public static readonly PropertyMap<BoundAttributeParameterReader> PropertyMap = new(
            new(nameof(BoundAttributeParameterDescriptor.Kind), ReadKind),
            new(nameof(BoundAttributeParameterDescriptor.Name), ReadName),
            new(nameof(BoundAttributeParameterDescriptor.TypeName), ReadTypeName),
            new(nameof(BoundAttributeParameterDescriptor.IsEnum), ReadIsEnum),
            new(nameof(BoundAttributeParameterDescriptor.Documentation), ReadDocumentation),
            new(nameof(BoundAttributeParameterDescriptor.Metadata), ReadMetadata),
            new(nameof(BoundAttributeParameterDescriptor.Diagnostics), ReadDiagnostics));

        public static void ReadKind(JsonReader reader, ref BoundAttributeParameterReader arg)
        {
            // In old serialized files, Kind might appear, though it isn't meaningful.
            _ = reader.ReadString();
        }

        public static void ReadName(JsonReader reader, ref BoundAttributeParameterReader arg)
            => arg.Builder.Name = Cached(reader.ReadString());

        public static void ReadTypeName(JsonReader reader, ref BoundAttributeParameterReader arg)
            => arg.Builder.TypeName = Cached(reader.ReadString());

        public static void ReadIsEnum(JsonReader reader, ref BoundAttributeParameterReader arg)
            => arg.Builder.IsEnum = reader.ReadBoolean();

        public static void ReadDocumentation(JsonReader reader, ref BoundAttributeParameterReader arg)
            => arg.Builder.Documentation = Cached(reader.ReadString());

        public static void ReadMetadata(JsonReader reader, ref BoundAttributeParameterReader arg)
            => reader.ProcessObject(arg.Builder.Metadata, ProcessMetadata);

        public static void ReadDiagnostics(JsonReader reader, ref BoundAttributeParameterReader arg)
            => reader.ProcessArray(arg.Builder.Diagnostics, ProcessDiagnostic);
    }
}
