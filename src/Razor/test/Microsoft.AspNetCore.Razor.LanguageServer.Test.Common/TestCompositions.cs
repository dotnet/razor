// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Reflection;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Common
{
    public static class TestCompositions
    {
        public static readonly TestComposition Compositions = TestComposition.Empty
            .AddAssemblies(MefHostServices.DefaultAssemblies)
            // Editor
            .AddAssemblies(Assembly.Load("Microsoft.VisualStudio.Text.Implementation, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"))
            .AddParts(typeof(TestExportJoinableTaskContext))
            // Roslyn
            .AddAssemblies(Assembly.LoadFrom("Microsoft.CodeAnalysis.LanguageServer.Protocol.dll"))
            .AddAssemblies(Assembly.LoadFrom("Microsoft.CodeAnalysis.ExternalAccess.Razor.dll"))
            .AddParts(typeof(RazorTestWorkspaceRegistrationService));
    }
}
