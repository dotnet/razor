// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal interface IProjectQueryService
{
    /// <summary>
    /// Returns all Razor project snapshots.
    /// </summary>
    IEnumerable<IProjectSnapshot> GetProjects();

    /// <summary>
    /// Returns all Razor project snapshots that contain the given document file path.
    /// </summary>
    ImmutableArray<IProjectSnapshot> FindProjects(string documentFilePath);
}
