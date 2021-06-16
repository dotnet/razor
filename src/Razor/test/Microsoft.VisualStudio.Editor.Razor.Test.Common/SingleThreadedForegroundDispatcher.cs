// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor
{
    internal class SingleThreadedForegroundDispatcher : ForegroundDispatcher
    {
        public SingleThreadedForegroundDispatcher()
        {
            SpecializedForegroundScheduler = SynchronizationContext.Current == null ? new ThrowingTaskScheduler() : TaskScheduler.FromCurrentSynchronizationContext();
            BackgroundScheduler = TaskScheduler.Default;
        }

        public override TaskScheduler SpecializedForegroundScheduler { get; }

        public override TaskScheduler BackgroundScheduler { get; }

        private Thread Thread { get; } = Thread.CurrentThread;

        public override bool IsSpecializedForegroundThread => Thread.CurrentThread == Thread;

        public override bool IsBackgroundThread => !IsSpecializedForegroundThread;

        private class ThrowingTaskScheduler : TaskScheduler
        {
            protected override IEnumerable<Task> GetScheduledTasks()
            {
                return Enumerable.Empty<Task>();
            }

            protected override void QueueTask(Task task)
            {
                throw new InvalidOperationException($"Use [{nameof(ForegroundFactAttribute)}]");
            }

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                throw new InvalidOperationException($"Use [{nameof(ForegroundFactAttribute)}]");
            }
        }
    }
}
