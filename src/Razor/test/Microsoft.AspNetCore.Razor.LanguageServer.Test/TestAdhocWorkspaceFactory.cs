// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

internal class TestAdhocWorkspaceFactory : IAdhocWorkspaceFactory
{
    public static readonly TestAdhocWorkspaceFactory Instance = new();

    private TestAdhocWorkspaceFactory()
    {
    }

    public AdhocWorkspace Create()
    {
        var services = TestServices.Create(workspaceServices: [], razorLanguageServices: []);
        var workspace = TestWorkspace.Create(services);
        return workspace;
    }
}
