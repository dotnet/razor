// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal interface IUpdateProjectAction
{
}

internal record HostProjectUpdatedAction(HostProject HostProject) : IUpdateProjectAction;

internal record ProjectWorkspaceStateChangedAction(ProjectWorkspaceState WorkspaceState) : IUpdateProjectAction;
