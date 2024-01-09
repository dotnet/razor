// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks;

public abstract partial class ProjectSnapshotManagerBenchmarkBase
{
    private class TestProjectSnapshotManagerDispatcher : IProjectSnapshotManagerDispatcher
    {
        public bool IsRunningOnScheduler => true;

        public TaskScheduler Scheduler => TaskScheduler.Default;
    }
}
