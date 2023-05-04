// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static partial class ObjectReaders
{
    public static RazorExtension ReadExtension(JsonReader reader)
        => reader.ReadNonNullObject(ReadExtensionFromProperties);

    public static RazorExtension ReadExtensionFromProperties(JsonReader reader)
    {
        var extensionName = reader.ReadNonNullString(nameof(RazorExtension.ExtensionName));

        return new SerializedRazorExtension(extensionName);
    }

    public static RazorConfiguration ReadConfigurationFromProperties(JsonReader reader)
    {
        ConfigurationData data = default;
        reader.ReadProperties(ref data, ConfigurationData.PropertyMap);

        return RazorConfiguration.Create(data.LanguageVersion, data.ConfigurationName, data.Extensions);
    }
}
