// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    /// <summary>
    /// Used for internal Visual Studio infrastructure. Not for public consumption.
    /// </summary>
    public abstract class NotifyRazorEditorCreated
    {
        /// <summary>
        /// Used for internal Visual Studio infrastructure. Not for public consumption.
        /// </summary>
        public abstract Task AddToWorkspaceAsync(IVsHierarchy hierarchy, uint itemId);
    }
}
