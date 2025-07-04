// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.VisualStudio.LanguageServices;
using RoslynWorkspace = Microsoft.CodeAnalysis.Workspace;

namespace Microsoft.VisualStudio.Extensibility.Testing;

internal partial class WorkspaceInProcess
{
    public async Task SetAutomaticSourceGeneratorExecutionAsync(CancellationToken cancellationToken)
    {
        var workspace = await GetComponentModelServiceAsync<VisualStudioWorkspace>(cancellationToken);
        RoslynTestAccessor.SetAutomaticSourceGeneratorExecution(workspace);
    }
}
