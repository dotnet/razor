// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.AspNetCore.Razor.Test.Common.Mef;

public sealed partial class TestComposition
{
    private const string RazorExternalAccessAssemblyDllName = "Microsoft.CodeAnalysis.ExternalAccess.Razor.dll";
    private const string RazorTestWorkspaceRegistrationServiceTypeName = "Microsoft.CodeAnalysis.ExternalAccess.Razor.RazorTestWorkspaceRegistrationService";

    private static Assembly? s_razorExternalAccessAssembly;
    private static Type? s_razorTestWorkspaceRegistrationServiceType;

    private static Assembly RazorExternalAccessAssembly
        => s_razorExternalAccessAssembly ?? InterlockedOperations.Initialize(ref s_razorExternalAccessAssembly,
            Assembly.LoadFrom(RazorExternalAccessAssemblyDllName));

    private static Type RazorTestWorkspaceRegistrationServiceType
        => s_razorTestWorkspaceRegistrationServiceType ?? InterlockedOperations.Initialize(ref s_razorTestWorkspaceRegistrationServiceType,
            RazorExternalAccessAssembly.GetType(RazorTestWorkspaceRegistrationServiceTypeName, throwOnError: true).AssumeNotNull());

    public static readonly TestComposition Roslyn = Empty
        .AddAssemblies(MefHostServices.DefaultAssemblies)
        .AddAssemblies(Assembly.LoadFrom("Microsoft.CodeAnalysis.dll"))
        .AddAssemblies(Assembly.LoadFrom("Microsoft.CodeAnalysis.CSharp.EditorFeatures.dll"))
        .AddAssemblies(Assembly.LoadFrom("Microsoft.CodeAnalysis.EditorFeatures.dll"))
        .AddAssemblies(RazorExternalAccessAssembly)
        .AddAssemblies(Assembly.LoadFrom("Microsoft.CodeAnalysis.LanguageServer.Protocol.dll"))
        .AddParts(RazorTestWorkspaceRegistrationServiceType);

    private const string VsTextImplementationAssemblyDllName = "Microsoft.VisualStudio.Text.Implementation.dll";

    private static Assembly? s_vsTextImplementationAssembly;

    private static Assembly VsTextImplementationAssembly
        => s_vsTextImplementationAssembly ?? InterlockedOperations.Initialize(ref s_vsTextImplementationAssembly,
            Assembly.LoadFrom(VsTextImplementationAssemblyDllName));

    public static readonly TestComposition Editor = Empty
        .AddAssemblies(VsTextImplementationAssembly)
        .AddParts(typeof(TestExportJoinableTaskContext));
}
