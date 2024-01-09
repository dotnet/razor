// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor;

internal interface IProjectSnapshotManagerDispatcher
{
    public bool IsRunningOnThread { get; }

    public TaskScheduler Scheduler { get; }
}
