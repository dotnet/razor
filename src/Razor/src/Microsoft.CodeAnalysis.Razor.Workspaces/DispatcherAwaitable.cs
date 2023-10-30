// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor;

internal readonly struct DispatcherAwaitable(ProjectSnapshotManagerDispatcher dispatcher, CancellationToken cancellationToken)
{
    public DispatcherAwaiter GetAwaiter()
    {
        return new(dispatcher, cancellationToken);
    }

    public readonly struct DispatcherAwaiter(ProjectSnapshotManagerDispatcher dispatcher, CancellationToken cancellationToken) : INotifyCompletion
    {
        public bool IsCompleted => dispatcher.IsDispatcherThread;

        public void OnCompleted(Action continuation)
        {
            _ = Task.Factory.StartNew(continuation, cancellationToken, TaskCreationOptions.None, dispatcher.DispatcherScheduler);
        }

        public void GetResult()
        {
        }
    }
}
