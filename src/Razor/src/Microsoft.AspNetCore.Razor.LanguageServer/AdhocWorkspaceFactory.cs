// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class AdhocWorkspaceFactory
    {
        public abstract AdhocWorkspace Create();

        public abstract AdhocWorkspace Create(IEnumerable<IWorkspaceService> workspaceServices);
    }
}
