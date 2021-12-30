﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem
{
    internal abstract class ProjectResolver
    {
        public abstract bool TryResolveProject(string documentFilePath, out ProjectSnapshot projectSnapshot, bool enforceDocumentInProject = true);

        public abstract ProjectSnapshot GetMiscellaneousProject();
    }
}
