﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor.Extensions;

internal static class IVsHierarchyExtensions
{
    public static async Task<string?> GetProjectFilePathAsync(this IVsHierarchy vsHierarchy, JoinableTaskFactory jtf, CancellationToken cancellationToken)
    {
        await jtf.SwitchToMainThreadAsync(cancellationToken);

        return vsHierarchy.GetProjectFilePath(jtf);
    }

    public static string? GetProjectFilePath(this IVsHierarchy vsHierarchy, JoinableTaskFactory jtf)
    {
        jtf.AssertUIThread();

        if (vsHierarchy is not IVsProject vsProject)
        {
            return null;
        }

        var hresult = vsProject.GetMkDocument((uint)VSConstants.VSITEMID.Root, out var projectFilePath);

        return ErrorHandler.Succeeded(hresult)
            ? projectFilePath
            : null;
    }
}
