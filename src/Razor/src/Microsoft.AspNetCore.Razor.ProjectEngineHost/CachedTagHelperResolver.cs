// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor;

internal class CachedTagHelperResolver(ITelemetryReporter telemetryReporter) : AbstractCachedResolver<TagHelperDescriptor>
{
    private readonly CompilationTagHelperResolver _innerResolver = new(telemetryReporter);

    protected override TagHelperDescriptor? TryGet(Checksum checksum)
    {
        TagHelperCache.Default.TryGet(checksum, out var tagHelper);
        return tagHelper;
    }

    protected override async ValueTask<ImmutableArray<Checksum>> GetCurrentChecksumsAsync(Project project, CancellationToken cancellationToken)
    {
        var projectPath = Path.GetDirectoryName(project.FilePath);
        if (projectPath is null)
        {
            return [];
        }

        var projectEngine = RazorProjectInfoHelpers.GetProjectEngine(project, projectPath);
        if (projectEngine is null)
        {
            return [];
        }

        var tagHelpers = await _innerResolver
            .GetTagHelpersAsync(project, projectEngine, cancellationToken)
            .ConfigureAwait(false);

        using var builder = new PooledArrayBuilder<Checksum>(capacity: tagHelpers.Length);

        // Add each tag helpers to the cache so that we can retrieve them later if needed.
        var cache = TagHelperCache.Default;

        foreach (var tagHelper in tagHelpers)
        {
            var checksum = tagHelper.Checksum;
            builder.Add(checksum);
            cache.TryAdd(checksum, tagHelper);
        }

        return builder.DrainToImmutable();
    }
}
