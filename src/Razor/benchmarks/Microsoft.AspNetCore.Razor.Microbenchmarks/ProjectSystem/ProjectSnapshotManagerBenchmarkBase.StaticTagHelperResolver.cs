// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks;

public abstract partial class ProjectSnapshotManagerBenchmarkBase
{
    private class StaticTagHelperResolver(ImmutableArray<TagHelperDescriptor> tagHelpers) : ITagHelperResolver
    {
        public ValueTask<TagHelperResolutionResult> GetTagHelpersAsync(
            Project workspaceProject,
            IProjectSnapshot projectSnapshot,
            CancellationToken cancellationToken)
            => new(new TagHelperResolutionResult(tagHelpers));
    }
}
