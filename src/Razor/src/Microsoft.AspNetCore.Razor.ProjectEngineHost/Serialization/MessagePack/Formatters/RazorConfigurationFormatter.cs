// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal sealed class RazorConfigurationFormatter : MessagePackFormatter<RazorConfiguration>
{
    public static readonly MessagePackFormatter<RazorConfiguration> Instance = new RazorConfigurationFormatter();

    private RazorConfigurationFormatter()
    {
    }

    public override RazorConfiguration Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();

        var configurationName = reader.DeserializeString(options);
        var languageVersionText = reader.DeserializeString(options);

        count -= 2;

        using var builder = new PooledArrayBuilder<RazorExtension>();

        for (var i = 0; i < count; i++)
        {
            var extensionName = reader.DeserializeString(options);
            builder.Add(new SerializedRazorExtension(extensionName));
        }

        var extensions = builder.ToArray();

        var languageVersion = RazorLanguageVersion.TryParse(languageVersionText, out var version)
            ? version
            : RazorLanguageVersion.Version_2_1;

        return RazorConfiguration.Create(languageVersion, configurationName, extensions);
    }

    public override void Serialize(ref MessagePackWriter writer, RazorConfiguration value, MessagePackSerializerOptions options)
    {
        // Write two values + one value per extension.
        var extensions = value.Extensions;
        var count = extensions.Count + 2;

        writer.WriteArrayHeader(count);

        writer.Write(value.ConfigurationName);

        if (value.LanguageVersion == RazorLanguageVersion.Experimental)
        {
            writer.Write(nameof(RazorLanguageVersion.Experimental));
        }
        else
        {
            writer.Write(value.LanguageVersion.ToString());
        }

        count -= 2;

        for (var i = 0; i < count; i++)
        {
            writer.Write(extensions[i].ExtensionName);
        }
    }
}
