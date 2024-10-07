// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Tooltip;

internal static class Extensions
{
    /// <summary>
    ///  Returns the Razor projects that contain the document specified by file path and a <see cref="bool"/>
    ///  that indicates whether or not the given tag helper is available within a project.
    /// </summary>
    internal static async Task<ImmutableArray<(IProjectSnapshot, bool IsAvailable)>> GetProjectAvailabilityAsync(
        this ISolutionQueryOperations solutionQueryOperations,
        string documentFilePath,
        string tagHelperTypeName,
        CancellationToken cancellationToken)
    {
        var projects = solutionQueryOperations.GetProjectsContainingDocument(documentFilePath);
        if (projects.IsEmpty)
        {
            return [];
        }

        using var result = new PooledArrayBuilder<(IProjectSnapshot, bool IsAvailable)>(capacity: projects.Length);

        foreach (var project in projects)
        {
            var containsTagHelper = await project.ContainsTagHelperAsync(tagHelperTypeName, cancellationToken).ConfigureAwait(false);

            result.Add((project, IsAvailable: containsTagHelper));
        }

        return result.DrainToImmutable();
    }

    internal static async Task<bool> ContainsTagHelperAsync(
        this IProjectSnapshot projectSnapshot,
        string tagHelperTypeName,
        CancellationToken cancellationToken)
    {
        var tagHelpers = await projectSnapshot.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);

        foreach (var tagHelper in tagHelpers)
        {
            if (tagHelper.GetTypeName() == tagHelperTypeName)
            {
                return true;
            }
        }

        return false;
    }
}
