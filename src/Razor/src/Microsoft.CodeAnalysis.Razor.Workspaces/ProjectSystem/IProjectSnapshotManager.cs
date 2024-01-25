// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal interface IProjectSnapshotManager : ILanguageService
{
    event EventHandler<ProjectChangeEventArgs> Changed;

    ImmutableArray<IProjectSnapshot> GetProjects();

    bool IsDocumentOpen(string documentFilePath);

    IProjectSnapshot? GetLoadedProject(ProjectKey projectKey);

    ImmutableArray<ProjectKey> GetAllProjectKeys(string projectFileName);
}
