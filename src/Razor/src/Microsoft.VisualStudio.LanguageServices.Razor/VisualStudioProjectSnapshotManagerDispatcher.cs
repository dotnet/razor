// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(ProjectSnapshotManagerDispatcher))]
    internal class VisualStudioProjectSnapshotManagerDispatcher : DefaultProjectSnapshotManagerDispatcher
    {
    }
}
