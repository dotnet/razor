// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.CodeAnalysis.Razor;

internal static class ProjectSnapshotManagerDispatcherExtensions
{
    public static void AssertDispatcherThread(this ProjectSnapshotManagerDispatcher dispatcher, [CallerMemberName] string? caller = null)
    {
        if (!dispatcher.IsDispatcherThread)
        {
            caller = caller is null ? "The method" : $"'{caller}'";
            throw new InvalidOperationException(caller + " must be called on the project snapshot manager's thread.");
        }
    }

    public static Task RunOnDispatcherThreadAsync(this ProjectSnapshotManagerDispatcher dispatcher, Action action, CancellationToken cancellationToken)
        => Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, dispatcher.DispatcherScheduler);

    public static Task RunOnDispatcherThreadAsync<TArg>(this ProjectSnapshotManagerDispatcher dispatcher, Action<TArg, CancellationToken> action, TArg arg, CancellationToken cancellationToken)
        => Task.Factory.StartNew(() => action(arg, cancellationToken), cancellationToken, TaskCreationOptions.None, dispatcher.DispatcherScheduler);

    public static Task<TResult> RunOnDispatcherThreadAsync<TResult>(this ProjectSnapshotManagerDispatcher dispatcher, Func<TResult> action, CancellationToken cancellationToken)
        => Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, dispatcher.DispatcherScheduler);
}
