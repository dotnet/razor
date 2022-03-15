// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    internal class ProjectSystemRazorExtension : RazorExtension
    {
        public ProjectSystemRazorExtension(string extensionName!!)
        {
            ExtensionName = extensionName;
        }

        public override string ExtensionName { get; }
    }
}
