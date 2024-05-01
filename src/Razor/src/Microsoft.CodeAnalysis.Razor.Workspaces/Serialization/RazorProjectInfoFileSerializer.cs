// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Resolvers;

namespace Microsoft.CodeAnalysis.Razor.Serialization;

internal class RazorProjectInfoFileSerializer : IRazorProjectInfoFileSerializer
{
    private static readonly MessagePackSerializerOptions s_options = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(
            RazorProjectInfoResolver.Instance,
            StandardResolver.Instance));

    public async Task<string> SerializeToTempFileAsync(RazorProjectInfo projectInfo, CancellationToken cancellationToken)
    {
        var filePath = Path.GetTempFileName();

        using var stream = File.OpenWrite(filePath);
        await MessagePackSerializer.SerializeAsync(stream, projectInfo, s_options, cancellationToken).ConfigureAwait(false);

        return filePath;
    }

    public async Task<RazorProjectInfo> DeserializeFromFileAndDeleteAsync(string filePath, CancellationToken cancellationToken)
    {
        RazorProjectInfo projectInfo;

        using (var stream = File.OpenRead(filePath))
        {
            projectInfo = await MessagePackSerializer.DeserializeAsync<RazorProjectInfo>(stream, s_options, cancellationToken).ConfigureAwait(false);
        }

        File.Delete(filePath);

        return projectInfo;
    }
}
