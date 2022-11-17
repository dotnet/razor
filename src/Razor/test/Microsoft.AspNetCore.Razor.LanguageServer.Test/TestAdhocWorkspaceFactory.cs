﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

internal class TestAdhocWorkspaceFactory : AdhocWorkspaceFactory
{
    public static readonly TestAdhocWorkspaceFactory Instance = new TestAdhocWorkspaceFactory();

    private TestAdhocWorkspaceFactory()
    {
    }

    public override AdhocWorkspace Create() => Create(Enumerable.Empty<IWorkspaceService>());

    public override AdhocWorkspace Create(IEnumerable<IWorkspaceService> workspaceServices)
    {
        var services = TestServices.Create(workspaceServices, Enumerable.Empty<ILanguageService>());
        var workspace = TestWorkspace.Create(services);
        return workspace;
    }
}
