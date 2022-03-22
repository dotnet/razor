// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor
{
    internal class DefaultTagHelperResolver : TagHelperResolver
    {
        public override Task<TagHelperResolutionResult> GetTagHelpersAsync(Project workspaceProject!!, ProjectSnapshot projectSnapshot!!, CancellationToken cancellationToken = default)
        {
            if (projectSnapshot.Configuration is null)
            {
                return Task.FromResult(TagHelperResolutionResult.Empty);
            }

            return GetTagHelpersAsync(workspaceProject, projectSnapshot.GetProjectEngine(), cancellationToken);
        }
    }
}
