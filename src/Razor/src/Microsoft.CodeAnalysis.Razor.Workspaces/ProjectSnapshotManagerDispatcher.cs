// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor;

internal abstract class ProjectSnapshotManagerDispatcher
{
    public abstract bool IsDispatcherThread { get; }

    public abstract TaskScheduler DispatcherScheduler { get; }

    public Task RunOnDispatcherThreadAsync(Action action, CancellationToken cancellationToken)
        => Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, DispatcherScheduler);

    public Task RunOnDispatcherThreadAsync<TArg>(Action<TArg, CancellationToken> action, TArg arg, CancellationToken cancellationToken)
        => Task.Factory.StartNew(() => action(arg, cancellationToken), cancellationToken, TaskCreationOptions.None, DispatcherScheduler);

    public Task<TResult> RunOnDispatcherThreadAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken)
        => Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, DispatcherScheduler);
}
