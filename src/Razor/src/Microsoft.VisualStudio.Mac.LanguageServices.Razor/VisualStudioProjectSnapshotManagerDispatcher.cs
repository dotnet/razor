﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    [Export(typeof(ProjectSnapshotManagerDispatcher))]
    internal class VisualStudioProjectSnapshotManagerDispatcher : DefaultProjectSnapshotManagerDispatcher
    {
    }
}
