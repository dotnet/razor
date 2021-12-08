// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal abstract class ProjectHierarchyInspector
    {
        public abstract bool HasCapability(string documentMoniker, IVsHierarchy hierarchy, string capability);
    }
}
