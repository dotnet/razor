﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Razor.ProjectSystem;

internal class TestProjectWorkspaceStateGenerator : IProjectWorkspaceStateGenerator
{
    private readonly List<TestUpdate> _updates;

    public TestProjectWorkspaceStateGenerator()
    {
        _updates = new List<TestUpdate>();
    }

    public IReadOnlyList<TestUpdate> Updates => _updates;

    public Task UpdateAsync(Project? workspaceProject, IProjectSnapshot projectSnapshot, CancellationToken cancellationToken)
    {
        var update = new TestUpdate(workspaceProject, projectSnapshot, cancellationToken);
        _updates.Add(update);

        return Task.CompletedTask;
    }

    public void Clear()
    {
        _updates.Clear();
    }

    public record TestUpdate(Project? WorkspaceProject, IProjectSnapshot ProjectSnapshot, CancellationToken CancellationToken)
    {
        public override string ToString()
        {
            using var _ = StringBuilderPool.GetPooledObject(out var builder);

            builder.Append($"{{{nameof(WorkspaceProject)} = ");

            if (WorkspaceProject is null)
            {
                builder.Append("<null>");
            }
            else
            {
                builder.Append(WorkspaceProject.Name);
            }

            builder.Append($", {nameof(ProjectSnapshot)} = {ProjectSnapshot.DisplayName}}}");

            return builder.ToString();
        }
    }
}
