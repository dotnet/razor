// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal static class IProjectSnapshotManagerExtensions
{
    public static IProjectSnapshot GetMiscellaneousProject(this IProjectSnapshotManager projectManager)
    {
        return projectManager.GetLoadedProject(MiscFilesHostProject.Instance.Key);
    }
}
