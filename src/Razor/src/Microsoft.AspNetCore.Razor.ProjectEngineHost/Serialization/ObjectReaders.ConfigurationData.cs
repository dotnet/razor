// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    private record struct ConfigurationData(string ConfigurationName, RazorLanguageVersion LanguageVersion, RazorExtension[] Extensions)
    {
        public static readonly PropertyMap<ConfigurationData> PropertyMap = new(
            (nameof(ConfigurationData.ConfigurationName), ReadConfigurationName),
            (nameof(ConfigurationData.LanguageVersion), ReadLanguageVersion),
            (nameof(ConfigurationData.Extensions), ReadExtensions));

        public static void ReadConfigurationName(JsonReader reader, ref ConfigurationData data)
            => data.ConfigurationName = reader.ReadNonNullString();

        public static void ReadLanguageVersion(JsonReader reader, ref ConfigurationData data)
        {
            string languageVersionValue;

            if (reader.TokenType == JsonToken.StartObject)
            {
                LanguageVersionData versionData = default;
                reader.ReadObjectData(ref versionData, LanguageVersionData.PropertyMap);
                languageVersionValue = $"{versionData.Major}.{versionData.Minor}";
            }
            else
            {
                languageVersionValue = reader.ReadNonNullString();
            }

            data.LanguageVersion = RazorLanguageVersion.TryParse(languageVersionValue, out var languageVersion)
                ? languageVersion
                : RazorLanguageVersion.Version_2_1;
        }

        public static void ReadExtensions(JsonReader reader, ref ConfigurationData data)
            => data.Extensions = reader.ReadArrayOrEmpty(ReadExtension);
    }
}
