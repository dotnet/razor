// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using OmniSharp.Extensions.JsonRpc;
using Xunit;
using static Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc.JsonRpcRequestScheduler;

namespace Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc
{
    public class JsonRpcRequestSchedulerTest : IDisposable
    {
        public JsonRpcRequestSchedulerTest()
        {
            Scheduler = new JsonRpcRequestScheduler(TestLoggerFactory.Instance);
            TestAccessor = Scheduler.GetTestAccessor();
        }

        private JsonRpcRequestScheduler Scheduler { get; }

        private TestAccessor TestAccessor { get; }

        [Fact]
        public void SerialRequests_RunInOrder()
        {
            // Arrange
            var manualResetEvent = new ManualResetEventSlim(initialState: false);
            var callOrder = new List<int>();
            var serialRequest1 = new ProcessSchedulerDelegate((cancellationToken) =>
            {
                callOrder.Add(1);
                return Task.CompletedTask;
            });
            var serialRequest2 = new ProcessSchedulerDelegate((cancellationToken) =>
            {
                callOrder.Add(2);
                manualResetEvent.Set();
                return Task.CompletedTask;
            });

            // Act
            Scheduler.Schedule(RequestProcessType.Serial, "SerialRequest1", serialRequest1);
            Scheduler.Schedule(RequestProcessType.Serial, "SerialRequest2", serialRequest2);

            // Assert
            Assert.True(manualResetEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1, 2 }, callOrder);
        }

        [Fact]
        public void SerialRequests_RunImmediatelyAfterLongRunningParallelRequests()
        {
            // Arrange
            var callOrder = new List<int>();
            var parallelRequest = new ProcessSchedulerDelegate(async (cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
                callOrder.Add(2);
            });
            var notifySerialRequest2Done = new ManualResetEventSlim(initialState: false);
            var serialRequest = new ProcessSchedulerDelegate((cancellationToken) =>
            {
                callOrder.Add(1);
                notifySerialRequest2Done.Set();
                return Task.CompletedTask;
            });

            // Act
            Scheduler.Schedule(RequestProcessType.Parallel, "ParallelRequest", parallelRequest);
            Scheduler.Schedule(RequestProcessType.Serial, "SerialRequest", serialRequest);

            // Assert
            Assert.True(notifySerialRequest2Done.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(new[] { 1 }, callOrder);
        }

        [Fact]
        public async Task SerialRequests_WaitsForPreviousSerialRequests()
        {
            // Arrange
            var blockSerialRequest1 = new AsyncManualResetEvent(initialState: false);
            var serialRequest1 = new ProcessSchedulerDelegate((cancellationToken) => blockSerialRequest1.WaitAsync(cancellationToken));
            var notifySerialRequest2Done = new AsyncManualResetEvent(initialState: false);
            var serialRequest2 = new ProcessSchedulerDelegate((cancellationToken) =>
            {
                notifySerialRequest2Done.Set();
                return Task.CompletedTask;
            });

            // Act
            Scheduler.Schedule(RequestProcessType.Serial, "SerialRequest1", serialRequest1);
            Scheduler.Schedule(RequestProcessType.Serial, "SerialRequest2", serialRequest2);

            // Assert
            Assert.False(notifySerialRequest2Done.IsSet);
            blockSerialRequest1.Set();
            using var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await notifySerialRequest2Done.WaitAsync(timeoutToken.Token);
        }

        [Fact]
        public async Task ParallelRequests_SynchronousDoNotBlockScheduler()
        {
            // Arrange
            var synchronousBlock = new ManualResetEventSlim(initialState: false);
            var parallelRequest1 = new ProcessSchedulerDelegate((cancellationToken) =>
            {
                synchronousBlock.Wait(cancellationToken);
                return Task.CompletedTask;
            });
            var notifyParallelRequest2Done = new AsyncManualResetEvent(initialState: false);
            var parallelRequest2 = new ProcessSchedulerDelegate((cancellationToken) =>
            {
                notifyParallelRequest2Done.Set();
                return Task.CompletedTask;
            });

            // Act
            Scheduler.Schedule(RequestProcessType.Parallel, "ParallelRequest1", parallelRequest1);
            Scheduler.Schedule(RequestProcessType.Parallel, "ParallelRequest2", parallelRequest2);

            // Assert
            using var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await notifyParallelRequest2Done.WaitAsync(timeoutToken.Token);
        }

        [Fact]
        public async Task Shutdown_StopsQueue()
        {
            // Arrange
            var serialRequest2Called = false;
            var serialRequest1 = new ProcessSchedulerDelegate((cancellationToken) => Task.Delay(TimeSpan.FromMinutes(5), cancellationToken));
            var serialRequest2 = new ProcessSchedulerDelegate((cancellationToken) =>
            {
                serialRequest2Called = true;
                return Task.CompletedTask;
            });
            Scheduler.Schedule(RequestProcessType.Serial, "SerialRequest1", serialRequest1);
            Scheduler.Schedule(RequestProcessType.Serial, "SerialRequest2", serialRequest2);

            // Act
            Scheduler.Dispose();

            // Assert
            using var timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Task.Run(() => TestAccessor.CompleteWorkAsync(), timeoutToken.Token);
            Assert.False(serialRequest2Called);
        }

        public void Dispose()
        {
            Scheduler.Dispose();
        }
    }
}
