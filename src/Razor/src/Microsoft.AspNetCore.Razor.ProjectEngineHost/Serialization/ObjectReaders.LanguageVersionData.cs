// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    private record struct LanguageVersionData(int Major, int Minor)
    {
        public static readonly PropertyMap<LanguageVersionData> PropertyMap = new(
            (nameof(Major), ReadMajor),
            (nameof(Minor), ReadMinor));

        private static void ReadMajor(JsonReader reader, ref LanguageVersionData data)
            => data.Major = reader.ReadInt32();

        private static void ReadMinor(JsonReader reader, ref LanguageVersionData data)
            => data.Minor = reader.ReadInt32();
    }
}
