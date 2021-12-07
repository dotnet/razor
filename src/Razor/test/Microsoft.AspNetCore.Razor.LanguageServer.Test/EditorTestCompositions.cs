// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Reflection;
using Microsoft.AspNetCore.Razor.Test.Common;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test
{
    public static class EditorTestCompositions
    {
        public static readonly TestComposition Editor = TestComposition.Empty
            .AddAssemblies(
                // Microsoft.VisualStudio.Text.Implementation.dll:
                Assembly.Load("Microsoft.VisualStudio.Text.Implementation, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"))
            .AddParts(
                typeof(TestExportJoinableTaskContext));
    }
}
