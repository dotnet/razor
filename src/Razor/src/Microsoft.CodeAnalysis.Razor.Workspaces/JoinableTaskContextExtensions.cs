// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal static class JoinableTaskContextExtensions
    {
        public static void AssertUIThread(this JoinableTaskContext joinableTaskContext, [CallerMemberName] string caller = null)
        {
            if (!joinableTaskContext.IsOnMainThread)
            {
                caller = caller == null ? Resources.ForegroundDispatcher_NoMethodNamePlaceholder : $"'{caller}'";
                throw new InvalidOperationException(Resources.FormatJoinableTaskContextExtensions_AssertUIThread(caller));
            }
        }

        public static async Task RunOnMainThreadAsync(
            this JoinableTaskContext joinableTaskContext,
            TaskScheduler originalScheduler,
            Action action,
            CancellationToken cancellationToken)
        {
            await joinableTaskContext.RunOnMainThreadAsync(
                originalScheduler, new Func<Task>(() => { action(); return Task.CompletedTask; }), cancellationToken);
        }

        public static async Task<TParam> RunOnMainThreadAsync<TParam>(
            this JoinableTaskContext joinableTaskContext,
            TaskScheduler originalScheduler,
            Func<TParam> action,
            CancellationToken cancellationToken)
        {
            await joinableTaskContext.Factory.SwitchToMainThreadAsync(cancellationToken);
            joinableTaskContext.AssertUIThread();
            var result = action();

            // Return to original thread
            await originalScheduler;
            return result;
        }
    }
}
