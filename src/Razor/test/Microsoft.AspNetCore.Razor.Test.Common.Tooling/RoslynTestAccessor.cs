// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.AspNetCore.Razor.Test.Common;

internal static class RoslynTestAccessor
{
    public static void SetAutomaticSourceGeneratorExecution(Workspace workspace)
    {
        TestRoslynOptionsHelper.SetAutomaticSourceGeneratorExecution(workspace);
    }
}
