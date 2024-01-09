// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor;

internal abstract class ProjectSnapshotManagerDispatcher
{
    public abstract bool IsDispatcherThread { get; }

    public abstract TaskScheduler DispatcherScheduler { get; }
}
