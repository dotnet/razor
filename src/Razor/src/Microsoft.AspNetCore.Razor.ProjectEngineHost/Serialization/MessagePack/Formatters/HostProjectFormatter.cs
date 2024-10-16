// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal sealed class HostProjectFormatter : ValueFormatter<HostProject>
{
    public static readonly ValueFormatter<HostProject> Instance = new HostProjectFormatter();

    private HostProjectFormatter()
    {
    }

    public override HostProject Deserialize(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        reader.ReadArrayHeaderAndVerify(5);

        var filePath = CachedStringFormatter.Instance.Deserialize(ref reader, options).AssumeNotNull();
        var intermediateOutputPath = CachedStringFormatter.Instance.Deserialize(ref reader, options).AssumeNotNull();
        var configuration = reader.Deserialize<RazorConfiguration>(options);
        var rootNamespace = CachedStringFormatter.Instance.Deserialize(ref reader, options);
        var displayName = CachedStringFormatter.Instance.Deserialize(ref reader, options).AssumeNotNull();

        return new HostProject(filePath, intermediateOutputPath, configuration, rootNamespace, displayName);
    }

    public override void Serialize(ref MessagePackWriter writer, HostProject value, SerializerCachingOptions options)
    {
        writer.WriteArrayHeader(5);

        CachedStringFormatter.Instance.Serialize(ref writer, value.FilePath, options);
        CachedStringFormatter.Instance.Serialize(ref writer, value.IntermediateOutputPath, options);
        writer.Serialize(value.Configuration, options);
        CachedStringFormatter.Instance.Serialize(ref writer, value.RootNamespace, options);
        CachedStringFormatter.Instance.Serialize(ref writer, value.DisplayName, options);
    }
}
