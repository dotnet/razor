// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Remote.Razor;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

[Shared]
[Export(typeof(IRemoteWorkspaceProvider))]
[Export(typeof(VSCodeRemoteWorkspaceProvider))]
internal class VSCodeRemoteWorkspaceProvider : IRemoteWorkspaceProvider
{
    private Workspace? _workspace;

    public void SetWorkspace(Workspace workspace)
    {
        _workspace = workspace;
    }

    public Workspace GetWorkspace()
    {
        return _workspace ?? throw new InvalidOperationException("Accessing the workspace before it has been provided");
    }
}
