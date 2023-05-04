// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;
using static Microsoft.AspNetCore.Razor.Language.RequiredAttributeDescriptor;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    private record struct RequiredAttributeReader(RequiredAttributeDescriptorBuilder Builder)
    {
        public static readonly PropertyMap<RequiredAttributeReader> PropertyMap = new(
            new(nameof(RequiredAttributeDescriptor.Name), ReadName),
            new(nameof(RequiredAttributeDescriptor.NameComparison), ReadNameComparison),
            new(nameof(RequiredAttributeDescriptor.Value), ReadValue),
            new(nameof(RequiredAttributeDescriptor.ValueComparison), ReadValueComparison),
            new(nameof(RequiredAttributeDescriptor.Metadata), ReadMetadata),
            new(nameof(RequiredAttributeDescriptor.Diagnostics), ReadDiagnostics));

        public static void ReadName(JsonReader reader, ref RequiredAttributeReader arg)
            => arg.Builder.Name = Cached(reader.ReadString());

        public static void ReadNameComparison(JsonReader reader, ref RequiredAttributeReader arg)
            => arg.Builder.NameComparisonMode = (NameComparisonMode)reader.ReadInt32();

        public static void ReadValue(JsonReader reader, ref RequiredAttributeReader arg)
            => arg.Builder.Value = Cached(reader.ReadString());

        public static void ReadValueComparison(JsonReader reader, ref RequiredAttributeReader arg)
            => arg.Builder.ValueComparisonMode = (ValueComparisonMode)reader.ReadInt32();

        public static void ReadMetadata(JsonReader reader, ref RequiredAttributeReader arg)
            => reader.ProcessObject(arg.Builder.Metadata, ProcessMetadata);

        public static void ReadDiagnostics(JsonReader reader, ref RequiredAttributeReader arg)
            => reader.ProcessArray(arg.Builder.Diagnostics, ProcessDiagnostic);
    }
}
