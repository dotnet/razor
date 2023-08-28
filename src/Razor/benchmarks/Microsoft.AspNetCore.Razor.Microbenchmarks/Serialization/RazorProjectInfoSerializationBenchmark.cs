// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization.Json;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.Serialization;

public class RazorProjectInfoSerializationBenchmark
{
    [ParamsAllValues]
    public ResourceSet ResourceSet { get; set; }

    private RazorProjectInfo ProjectInfo
        => ResourceSet switch
        {
            ResourceSet.Telerik => CommonResources.TelerikProjectInfo,
            _ => CommonResources.LegacyProjectInfo
        };

    private byte[] ProjectRazorJsonBytes
        => ResourceSet switch
        {
            ResourceSet.Telerik => CommonResources.TelerikProjectRazorJsonBytes,
            _ => CommonResources.LegacyProjectRazorJsonBytes
        };

    private static RazorProjectInfo DeserializeProjectInfo(TextReader reader)
    {
        return JsonDataConvert.DeserializeData(reader,
            static r => r.ReadNonNullObject(ObjectReaders.ReadRazorProjectInfoFromProperties));
    }

    private static void SerializeProjectInfo(TextWriter writer, RazorProjectInfo projectInfo)
    {
        JsonDataConvert.SerializeObject(writer, projectInfo, ObjectWriters.WriteProperties);
    }

    [Benchmark(Description = "Serialize RazorProjectInfo")]
    public void Serialize()
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096);

        SerializeProjectInfo(writer, ProjectInfo);
    }

    [Benchmark(Description = "Deserialize RazorProjectInfo")]
    public void Deserialize()
    {
        using var stream = new MemoryStream(ProjectRazorJsonBytes);
        using var reader = new StreamReader(stream);

        var projectInfo = DeserializeProjectInfo(reader);

        if (projectInfo.ProjectWorkspaceState is null ||
            projectInfo.ProjectWorkspaceState.TagHelpers.Length != ProjectInfo.ProjectWorkspaceState?.TagHelpers.Length)
        {
            throw new InvalidDataException();
        }
    }

    [Benchmark(Description = "RoundTrip RazorProjectInfo")]
    public void RoundTrip()
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
        {
            SerializeProjectInfo(writer, ProjectInfo);
        }

        stream.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(stream);

        var projectInfo = DeserializeProjectInfo(reader);

        if (projectInfo.ProjectWorkspaceState is null ||
            projectInfo.ProjectWorkspaceState.TagHelpers.Length != ProjectInfo.ProjectWorkspaceState?.TagHelpers.Length)
        {
            throw new InvalidDataException();
        }
    }
}
