// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Tooltip;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;

internal static class Extensions
{
    internal static async Task<string?> GetProjectAvailabilityTextAsync(
        this ISolutionQueryOperations solutionQueryOperations,
        string documentFilePath,
        string tagHelperTypeName,
        CancellationToken cancellationToken)
    {
        var projects = await solutionQueryOperations
            .GetProjectAvailabilityAsync(documentFilePath, tagHelperTypeName, cancellationToken)
            .ConfigureAwait(false);

        if (projects.IsEmpty)
        {
            return null;
        }

        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        foreach (var (project, isAvailable) in projects)
        {
            if (isAvailable)
            {
                continue;
            }

            if (builder.Length == 0)
            {
                builder.AppendLine();
                builder.Append($"⚠️ {SR.Not_Available_In}:");
            }

            builder.AppendLine();
            builder.Append("    ");
            builder.Append(project.DisplayName);
        }

        if (builder.Length == 0)
        {
            return null;
        }

        return builder.ToString();
    }
}
