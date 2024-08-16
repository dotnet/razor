// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.RoslynWorkspace;

public abstract partial class RazorWorkspaceListenerBase
{
    internal record Work(ProjectId ProjectId);
    internal record UpdateWork(ProjectId ProjectId) : Work(ProjectId);
    internal record RemovalWork(ProjectId ProjectId, string IntermediateOutputPath) : Work(ProjectId);
}
