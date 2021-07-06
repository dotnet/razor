// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test
{
    internal class TestLanguageServerWorkspaceFactory : LanguageServerWorkspaceFactory
    {
        public static readonly TestLanguageServerWorkspaceFactory Instance = new TestLanguageServerWorkspaceFactory();

        private TestLanguageServerWorkspaceFactory()
        {
        }

        public override LanguageServerWorkspace Create() => Create(Enumerable.Empty<IWorkspaceService>());

        public override LanguageServerWorkspace Create(IEnumerable<IWorkspaceService> workspaceServices)
        {
            var services = TestServices.Create(workspaceServices, Enumerable.Empty<ILanguageService>());
            var workspace = new LanguageServerWorkspace(services);
            return workspace;
        }
    }
}
