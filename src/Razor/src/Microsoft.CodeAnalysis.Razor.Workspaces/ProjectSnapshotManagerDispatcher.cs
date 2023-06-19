// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor;

internal abstract class ProjectSnapshotManagerDispatcher
{
    public abstract bool IsDispatcherThread { get; }

    public abstract TaskScheduler DispatcherScheduler { get; }

    public Task RunOnDispatcherThreadAsync(Action action, CancellationToken _)
    {
        action();
        return Task.CompletedTask;
    }

    public Task RunOnDispatcherThreadAsync<TArg>(Action<TArg, CancellationToken> action, TArg arg, CancellationToken cancellationToken)
    {
        action(arg, cancellationToken);
        return Task.CompletedTask;
    }

    public Task<TResult> RunOnDispatcherThreadAsync<TResult>(Func<TResult> action, CancellationToken _)
    {
        return Task.FromResult(action());
    }

    public virtual void AssertDispatcherThread([CallerMemberName] string? _ = null)
    {
        //if (!IsDispatcherThread)
        //{
        //    caller = caller is null ? "The method" : $"'{caller}'";
        //    throw new InvalidOperationException(caller + " must be called on the project snapshot manager's thread.");
        //}
    }
}
