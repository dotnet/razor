// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.LiveShare.Razor
{
    public sealed class ProjectSnapshotManagerProxyState
    {
        public ProjectSnapshotManagerProxyState(IReadOnlyList<ProjectSnapshotHandleProxy> projectHandles!!)
        {
            ProjectHandles = projectHandles;
        }

        public IReadOnlyList<ProjectSnapshotHandleProxy> ProjectHandles { get; }
    }
}
