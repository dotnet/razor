// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor
{
    internal abstract class ForegroundDispatcher
    {
        public abstract bool IsForegroundThread { get; }

        public abstract TaskScheduler ForegroundScheduler { get; }

        public Task RunOnForegroundAsync(Action action, CancellationToken cancellationToken)
            => Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, ForegroundScheduler);

        public Task<TResult> RunOnForegroundAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken)
            => Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, ForegroundScheduler);

        public virtual void AssertForegroundThread([CallerMemberName] string caller = null)
        {
            if (!IsForegroundThread)
            {
                caller = caller == null ? Workspaces.Resources.ForegroundDispatcher_NoMethodNamePlaceholder : $"'{caller}'";
                throw new InvalidOperationException(Workspaces.Resources.FormatForegroundDispatcher_AssertForegroundThread(caller));
            }
        }
    }
}
