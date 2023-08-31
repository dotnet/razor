﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal abstract class ProjectSnapshotManagerAccessor
{
    public abstract ProjectSnapshotManagerBase Instance { get; }
}
