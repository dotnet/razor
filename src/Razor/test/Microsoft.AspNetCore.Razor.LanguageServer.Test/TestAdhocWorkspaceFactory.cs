// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Test.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

internal class TestAdhocWorkspaceFactory : AdhocWorkspaceFactory
{
    public static readonly TestAdhocWorkspaceFactory Instance = new TestAdhocWorkspaceFactory();

    private TestAdhocWorkspaceFactory()
    {
    }

    public override AdhocWorkspace Create() => Create(Array.Empty<IWorkspaceService>());

    public override AdhocWorkspace Create(params IWorkspaceService[] workspaceServices)
    {
        var services = TestServices.Create(workspaceServices, Enumerable.Empty<ILanguageService>());
        var workspace = TestWorkspace.Create(services);
        return workspace;
    }
}
