// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    public class BatchingWorkQueueTest : IDisposable
    {
        public BatchingWorkQueueTest()
        {
            ErrorReporter = new TestErrorReporter();
            WorkQueue = new BatchingWorkQueue(TimeSpan.FromMilliseconds(1), StringComparer.Ordinal, ErrorReporter);
            TestAccessor = WorkQueue.GetTestAccessor();
            TestAccessor.NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false);
        }

        private BatchingWorkQueue WorkQueue { get; }

        private BatchingWorkQueue.TestAccessor TestAccessor { get; }

        private TestErrorReporter ErrorReporter { get; }

        [Fact]
        public void Enqueue_ProcessesNotifications_AndGoesBackToSleep()
        {
            // Arrange
            var workItem = new TestBatchableWorkItem();
            TestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);
            TestAccessor.BlockBackgroundWorkCompleting = new ManualResetEventSlim(initialState: false);

            // Act
            WorkQueue.Enqueue("key", workItem);

            // Assert
            Assert.True(TestAccessor.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.NotEmpty(TestAccessor.Work);

            // Allow the background work to proceed.
            TestAccessor.BlockBackgroundWorkStart.Set();
            TestAccessor.BlockBackgroundWorkCompleting.Set();

            TestAccessor.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3));

            Assert.False(TestAccessor.IsScheduledOrRunning, "Queue should not have restarted");
            Assert.Empty(TestAccessor.Work);
            Assert.True(workItem.Processed);
            Assert.Empty(ErrorReporter.ReportedExceptions);
        }

        [Fact]
        public void Enqueue_BatchesNotificationsByKey_ProcessesLast()
        {
            // Arrange
            var originalWorkItem = new ThrowingBatchableWorkItem();
            var newestWorkItem = new TestBatchableWorkItem();
            TestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

            // Act
            WorkQueue.Enqueue("key", originalWorkItem);
            WorkQueue.Enqueue("key", newestWorkItem);

            // Assert
            Assert.True(TestAccessor.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.NotEmpty(TestAccessor.Work);

            // Allow the background work to start.
            TestAccessor.BlockBackgroundWorkStart.Set();
            TestAccessor.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3));

            Assert.Empty(TestAccessor.Work);

            Assert.False(originalWorkItem.Processed);
            Assert.True(newestWorkItem.Processed);
            Assert.Empty(ErrorReporter.ReportedExceptions);
        }

        [Fact]
        public void Enqueue_ProcessesNotifications_AndRestarts()
        {
            // Arrange
            var initialWorkItem = new TestBatchableWorkItem();
            var workItemToCauseRestart = new TestBatchableWorkItem();
            TestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);
            TestAccessor.NotifyBackgroundWorkStarting = new ManualResetEventSlim(initialState: false);
            TestAccessor.NotifyBackgroundCapturedWorkload = new ManualResetEventSlim(initialState: false);
            TestAccessor.BlockBackgroundWorkCompleting = new ManualResetEventSlim(initialState: false);
            TestAccessor.NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false);

            // Act & Assert
            WorkQueue.Enqueue("key", initialWorkItem);

            Assert.True(TestAccessor.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.NotEmpty(TestAccessor.Work);

            // Allow the background work to start.
            TestAccessor.BlockBackgroundWorkStart.Set();

            TestAccessor.NotifyBackgroundWorkStarting.Wait(TimeSpan.FromSeconds(3));

            Assert.True(TestAccessor.IsScheduledOrRunning, "Worker should be processing now");

            TestAccessor.NotifyBackgroundCapturedWorkload.Wait(TimeSpan.FromSeconds(3));
            Assert.Empty(TestAccessor.Work);

            WorkQueue.Enqueue("key", workItemToCauseRestart);
            Assert.NotEmpty(TestAccessor.Work); // Now we should see the worker restart when it finishes.

            // Allow work to complete, which should restart the timer.
            TestAccessor.BlockBackgroundWorkCompleting.Set();

            TestAccessor.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3));
            TestAccessor.NotifyBackgroundWorkCompleted.Reset();

            // It should start running again right away.
            Assert.True(TestAccessor.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.NotEmpty(TestAccessor.Work);

            // Allow the background work to proceed.
            TestAccessor.BlockBackgroundWorkStart.Set();

            TestAccessor.BlockBackgroundWorkCompleting.Set();
            TestAccessor.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3));

            Assert.False(TestAccessor.IsScheduledOrRunning, "Queue should not have restarted");
            Assert.Empty(TestAccessor.Work);
            Assert.True(initialWorkItem.Processed);
            Assert.True(workItemToCauseRestart.Processed);
            Assert.Empty(ErrorReporter.ReportedExceptions);
        }

        [Fact]
        public void Enqueue_ThrowingWorkItem_DoesNotPreventProcessingSubsequentItems()
        {
            // Arrange
            var throwingWorkItem = new ThrowingBatchableWorkItem();
            var validWorkItem = new TestBatchableWorkItem();
            TestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

            // Act
            WorkQueue.Enqueue("key", throwingWorkItem);
            WorkQueue.Enqueue("key2", validWorkItem);

            // Assert
            Assert.True(TestAccessor.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.NotEmpty(TestAccessor.Work);

            // Allow the background work to start.
            TestAccessor.BlockBackgroundWorkStart.Set();
            TestAccessor.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3));

            Assert.Empty(TestAccessor.Work);

            Assert.True(throwingWorkItem.Processed);
            Assert.True(validWorkItem.Processed);
            Assert.Single(ErrorReporter.ReportedExceptions);
        }

        [Fact]
        public void Enqueue_DisposedPreventsRestart()
        {
            // Arrange
            var initialWorkItem = new TestBatchableWorkItem();
            var workItemToCauseRestart = new TestBatchableWorkItem();
            TestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);
            TestAccessor.NotifyBackgroundWorkStarting = new ManualResetEventSlim(initialState: false);
            TestAccessor.NotifyBackgroundCapturedWorkload = new ManualResetEventSlim(initialState: false);
            TestAccessor.NotifyBackgroundWorkCompleting = new ManualResetEventSlim(initialState: false);
            TestAccessor.BlockBackgroundWorkCompleting = new ManualResetEventSlim(initialState: false);
            TestAccessor.NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false);

            // Act & Assert
            WorkQueue.Enqueue("key", initialWorkItem);

            Assert.True(TestAccessor.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.NotEmpty(TestAccessor.Work);

            // Allow the background work to start.
            TestAccessor.BlockBackgroundWorkStart.Set();

            TestAccessor.NotifyBackgroundWorkStarting.Wait(TimeSpan.FromSeconds(3));

            Assert.True(TestAccessor.IsScheduledOrRunning, "Worker should be processing now");

            TestAccessor.NotifyBackgroundCapturedWorkload.Wait(TimeSpan.FromSeconds(3));
            Assert.Empty(TestAccessor.Work);

            // Wait for the background workload to complete
            TestAccessor.NotifyBackgroundWorkCompleting.Wait(TimeSpan.FromSeconds(5));

            WorkQueue.Enqueue("key", workItemToCauseRestart);
            Assert.NotEmpty(TestAccessor.Work);

            // Disposing before the queue has a chance to restart;
            WorkQueue.Dispose();

            // Allow work to complete, which should restart the timer.
            TestAccessor.BlockBackgroundWorkCompleting.Set();

            TestAccessor.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3));
            TestAccessor.NotifyBackgroundWorkCompleted.Reset();

            // It should start running again right away.
            Assert.False(TestAccessor.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");

            // Dispose clears the work queue
            Assert.Empty(TestAccessor.Work);

            Assert.True(initialWorkItem.Processed);
            Assert.False(workItemToCauseRestart.Processed);
            Assert.Empty(ErrorReporter.ReportedExceptions);
        }

        public void Dispose()
        {
            WorkQueue.Dispose();
        }

        private class TestBatchableWorkItem : BatchableWorkItem
        {
            public bool Processed { get; private set; }

            public override ValueTask ProcessAsync(CancellationToken cancellationToken)
            {
                Processed = true;
                return new ValueTask();
            }
        }

        private class ThrowingBatchableWorkItem : TestBatchableWorkItem
        {
            public override ValueTask ProcessAsync(CancellationToken cancellationToken)
            {
                base.ProcessAsync(cancellationToken);
                throw new InvalidOperationException();
            }
        }

        private class TestErrorReporter : ErrorReporter
        {
            private readonly List<Exception> _reportedExceptions = new List<Exception>();

            public IReadOnlyList<Exception> ReportedExceptions => _reportedExceptions;

            public override void ReportError(Exception exception) => _reportedExceptions.Add(exception);

            public override void ReportError(Exception exception, ProjectSnapshot project) => _reportedExceptions.Add(exception);

            public override void ReportError(Exception exception, Project workspaceProject) => _reportedExceptions.Add(exception);
        }
    }
}
