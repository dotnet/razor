// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class DefaultLSPProgressListenerTest
    {
        [Fact]
        public void TryListenForProgress_ReturnsTrue()
        {
            // Arrange
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var token = Guid.NewGuid().ToString();
            var notificationTimeout = TimeSpan.FromSeconds(2);
            var cts = new CancellationTokenSource();

            var lspProgressListener = new DefaultLSPProgressListener(languageServiceBroker);

            // Act
            var listenerAdded = lspProgressListener.TryListenForProgress(
                token,
                onProgressNotifyAsync: async (value, ct) => { await Task.Delay(5); },
                notificationTimeout,
                cts.Token,
                out var onCompleted);

            // Assert
            Assert.True(listenerAdded);
            Assert.NotNull(onCompleted);
            Assert.False(onCompleted.IsCompleted);
        }

        [Fact]
        public void TryListenForProgress_DuplicateRegistration_ReturnsFalse()
        {
            // Arrange
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var token = Guid.NewGuid().ToString();
            var notificationTimeout = TimeSpan.FromSeconds(2);
            var cts = new CancellationTokenSource();

            var lspProgressListener = new DefaultLSPProgressListener(languageServiceBroker);

            // Act
            _ = lspProgressListener.TryListenForProgress(
                token,
                onProgressNotifyAsync: async (value, ct) => { await Task.Delay(5); },
                notificationTimeout,
                cts.Token,
                out _);
            var listenerAdded = lspProgressListener.TryListenForProgress(
                token,
                onProgressNotifyAsync: async (value, ct) => { await Task.Delay(5); },
                notificationTimeout,
                cts.Token,
                out var onCompleted);

            // Assert
            Assert.False(listenerAdded);
            Assert.Null(onCompleted);
        }

        [Fact]
        public async Task TryListenForProgress_TaskNotificationTimeoutAfterNoInitialProgress()
        {
            // Arrange
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var token = Guid.NewGuid().ToString();
            var notificationTimeout = TimeSpan.FromSeconds(2);
            var cts = new CancellationTokenSource();

            var lspProgressListener = new DefaultLSPProgressListener(languageServiceBroker);

            // Act 1
            var listenerAdded = lspProgressListener.TryListenForProgress(
                token,
                onProgressNotifyAsync: async (value, ct) => { await Task.Delay(5); },
                notificationTimeout,
                cts.Token,
                out var onCompleted);

            // Assert 1
            Assert.True(listenerAdded);
            Assert.NotNull(onCompleted);
            Assert.False(onCompleted.IsCompleted);

            // Act 2
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Assert 2
            Assert.True(onCompleted.IsCompleted);
        }

        [Fact]
        public async Task TryListenForProgress_ProgressNotificationInvalid()
        {
            // Arrange
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var token = Guid.NewGuid().ToString();
            var notificationTimeout = TimeSpan.FromSeconds(2);
            var cts = new CancellationTokenSource();

            var lspProgressListener = new DefaultLSPProgressListener(languageServiceBroker);

            // Act
            var listenerAdded = lspProgressListener.TryListenForProgress(
                token,
                onProgressNotifyAsync: async (value, ct) => { await Task.Delay(5); },
                notificationTimeout,
                cts.Token,
                out var onCompleted);

            await Task.Delay(500);

            await lspProgressListener.ProcessProgressNotificationAsync(Methods.ClientRegisterCapabilityName, new JObject());

            await Task.Delay(1600); // Note 500 + 1600 = 2100ms > 2000ms notification timeout

            // Assert
            Assert.True(onCompleted.IsCompleted);
        }

        [Fact]
        public async Task TryListenForProgress_SingleProgressNotificationReported()
        {
            // Arrange
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var token = Guid.NewGuid().ToString();
            var notificationTimeout = TimeSpan.FromSeconds(2);
            var cts = new CancellationTokenSource();

            var expectedValue = "abcxyz";
            var parameterToken = new JObject
            {
                { Methods.ProgressNotificationTokenName, token },
                { "value", JArray.FromObject(new[] { expectedValue }) }
            };

            var onProgressNotifyAsyncCalled = false;
            Func<JToken, CancellationToken, Task> onProgressNotifyAsync = (value, ct) => {
                var result = value.ToObject<string[]>();
                var firstValue = Assert.Single(result);
                Assert.Equal(expectedValue, firstValue);
                onProgressNotifyAsyncCalled = true;
                return Task.CompletedTask;
            };

            var lspProgressListener = new DefaultLSPProgressListener(languageServiceBroker);

            // Act 1
            var listenerAdded = lspProgressListener.TryListenForProgress(
                token,
                onProgressNotifyAsync: onProgressNotifyAsync,
                notificationTimeout,
                cts.Token,
                out var onCompleted);

            await Task.Delay(500);

            await lspProgressListener.ProcessProgressNotificationAsync(Methods.ProgressNotificationName, parameterToken);

            await Task.Delay(1500);

            // Assert 1
            Assert.False(onCompleted.IsCompleted, "Task shouldn't complete for 2s after last notification");

            // Act 2
            await onCompleted;

            // Assert 2
            Assert.True(onProgressNotifyAsyncCalled);
        }

        [Fact]
        public async Task TryListenForProgress_MultipleProgressNotificationReported()
        {
            // Arrange
            const int NUM_NOTIFICATIONS = 50;
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var token = Guid.NewGuid().ToString();
            var notificationTimeout = TimeSpan.FromSeconds(2);
            var cts = new CancellationTokenSource();

            var lspProgressListener = new DefaultLSPProgressListener(languageServiceBroker);

            var parameterTokens = new List<JObject>();
            for (var i = 0; i < NUM_NOTIFICATIONS; ++i)
            {
                parameterTokens.Add(new JObject
                {
                    { Methods.ProgressNotificationTokenName, token },
                    { "value", i }
                });
            }

            var receivedResults = new List<int>();
            Func<JToken, CancellationToken, Task> onProgressNotifyAsync = (value, ct) => {
                receivedResults.Add(value.ToObject<int>());
                return Task.CompletedTask;
            };

            // Act 1
            var listenerAdded = lspProgressListener.TryListenForProgress(
                token,
                onProgressNotifyAsync: onProgressNotifyAsync,
                notificationTimeout,
                cts.Token,
                out var onCompleted);

            await Task.Delay(500);

            Parallel.ForEach(parameterTokens, parameterToken =>
            {
                _ = lspProgressListener.ProcessProgressNotificationAsync(Methods.ProgressNotificationName, parameterToken);
            });

            await Task.Delay(1500);

            // Assert 1
            Assert.False(onCompleted.IsCompleted, "Task shouldn't complete for 2s after last notification");

            // Act 2
            await onCompleted;

            // Assert 2
            receivedResults.Sort();
            for (var i = 0; i < NUM_NOTIFICATIONS; ++i)
            {
                Assert.Equal(i, receivedResults[i]);
            }
        }
    }
}
