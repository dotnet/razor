// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Client;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.JsonRpc.Server.Messages;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc
{
    public class RazorOmniSharpRequestInvokerTest
    {
        public RazorOmniSharpRequestInvokerTest()
        {
            Options = new RequestInvokerOptions(
                requestTimeout: TimeSpan.FromSeconds(10),
                supportContentModified: false,
                concurrency: int.MaxValue);
            OutputHandler = new TestOutputHandler();
            RequestRouter = new TestRequestRouter(routeDelay: TimeSpan.Zero);
            RequestDescriptor = new TestRequestDescriptor("textDocument/didOpen");
            Request = new Request(id: "serial", RequestDescriptor.Default.Method, @params: null);
            NotificationDescriptor = new TestRequestDescriptor("textDocument/didChange");
            Notification = new Notification(NotificationDescriptor.Default.Method, @params: null);
        }

        private Notification Notification { get; }

        private IRequestDescriptor<IHandlerDescriptor> NotificationDescriptor { get; }

        private Request Request { get; }

        private IRequestDescriptor<IHandlerDescriptor> RequestDescriptor { get; }

        private RequestInvokerOptions Options { get; }

        private TestOutputHandler OutputHandler { get; }

        private TestRequestRouter RequestRouter { get; }

        [Fact]
        public async Task InvokeRequest_RoutesRequest()
        {
            // Arrange
            using var requestInvoker = CreateRequestInvoker();

            // Act
            var handle = requestInvoker.InvokeRequest(RequestDescriptor, Request);
            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WaitForCompletionAsync(handle, timeoutTokenSource.Token).ConfigureAwait(false);

            // Assert
            var response = Assert.Single(OutputHandler.SentItems);
            Assert.IsType<TestSuccessfulResponse>(response);
        }

        [Fact]
        public async Task InvokeRequest_CanTimeout()
        {
            // Arrange
            var requestRouter = new TestRequestRouter(routeDelay: TimeSpan.FromMinutes(1));
            using var requestInvoker = CreateRequestInvoker(requestTimeout: TimeSpan.FromMilliseconds(1), requestRouter);

            // Act
            var handle = requestInvoker.InvokeRequest(RequestDescriptor, Request);
            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WaitForCompletionAsync(handle, timeoutTokenSource.Token).ConfigureAwait(false);

            // Assert
            var response = Assert.Single(OutputHandler.SentItems);
            Assert.IsType<RequestCancelled>(response);
        }

        [Fact]
        public async Task InvokeRequest_CanBeCancelled()
        {
            // Arrange
            var requestRouter = new TestRequestRouter(routeDelay: TimeSpan.FromMinutes(1));
            using var requestInvoker = CreateRequestInvoker(requestRouter: requestRouter);
            var handle = requestInvoker.InvokeRequest(RequestDescriptor, Request);

            // Act
            handle.CancellationTokenSource.Cancel();
            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WaitForCompletionAsync(handle, timeoutTokenSource.Token).ConfigureAwait(false);

            // Assert
            var response = Assert.Single(OutputHandler.SentItems);
            Assert.IsType<RequestCancelled>(response);
        }

        [Fact]
        public async Task InvokeRequest_ShutdownCancels()
        {
            // Arrange
            var requestRouter = new TestRequestRouter(routeDelay: TimeSpan.FromMinutes(1));
            using var requestInvoker = CreateRequestInvoker(requestRouter: requestRouter);
            var handle = requestInvoker.InvokeRequest(RequestDescriptor, Request);

            // Act
            requestInvoker.Dispose(); // Shutdown the invoker

            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WaitForCompletionAsync(handle, timeoutTokenSource.Token).ConfigureAwait(false);

            // Assert
            var response = Assert.Single(OutputHandler.SentItems);
            Assert.IsType<RequestCancelled>(response);
        }

        [Fact]
        public async Task InvokeRequest_UnexpectedError()
        {
            // Arrange
            var requestRouter = new TestRequestRouter(onRequestCallback: () => throw new InvalidOperationException());
            using var requestInvoker = CreateRequestInvoker(requestRouter: requestRouter);

            // Act
            var handle = requestInvoker.InvokeRequest(RequestDescriptor, Request);
            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WaitForCompletionAsync(handle, timeoutTokenSource.Token).ConfigureAwait(false);

            // Assert
            var response = Assert.Single(OutputHandler.SentItems);
            Assert.IsType<InternalError>(response);
        }

        [Fact]
        public async Task InvokeNotification_RoutesNotification()
        {
            // Arrange
            using var requestInvoker = CreateRequestInvoker();
            var asyncResetEvent = new AsyncManualResetEvent(initialState: false);
            RequestRouter.OnNotificationCompleted += (notification) =>
            {
                asyncResetEvent.Set();
            };

            // Act
            requestInvoker.InvokeNotification(NotificationDescriptor, Notification);

            // Assert
            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await asyncResetEvent.WaitAsync(timeoutTokenSource.Token).ConfigureAwait(false);
            Assert.Empty(OutputHandler.SentItems);
        }

        [Fact]
        public async Task InvokeNotification_CanTimeout()
        {
            // Arrange
            var requestRouter = new TestRequestRouter(routeDelay: TimeSpan.FromMinutes(1));
            using var requestInvoker = CreateRequestInvoker(requestTimeout: TimeSpan.FromMilliseconds(1), requestRouter);
            var asyncResetEvent = new AsyncManualResetEvent(initialState: false);
            requestRouter.OnNotificationCancelled += (notification) =>
            {
                asyncResetEvent.Set();
            };

            // Act
            requestInvoker.InvokeNotification(NotificationDescriptor, Notification);

            // Assert
            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await asyncResetEvent.WaitAsync(timeoutTokenSource.Token).ConfigureAwait(false);
            Assert.Empty(OutputHandler.SentItems);
        }

        [Fact]
        public async Task InvokeNotification_ShutdownCancels()
        {
            // Arrange
            var requestRouter = new TestRequestRouter(routeDelay: TimeSpan.FromMinutes(1));
            using var requestInvoker = CreateRequestInvoker(requestRouter: requestRouter);
            var asyncResetEvent = new AsyncManualResetEvent(initialState: false);
            requestRouter.OnNotificationCancelled += (notification) =>
            {
                asyncResetEvent.Set();
            };
            requestInvoker.InvokeNotification(NotificationDescriptor, Notification);

            // Act
            requestInvoker.Dispose(); // Shutdown the invoker

            // Assert
            using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await asyncResetEvent.WaitAsync(timeoutTokenSource.Token).ConfigureAwait(false);
            Assert.Empty(OutputHandler.SentItems);
        }

        private Task WaitForCompletionAsync(RequestInvocationHandle handle, CancellationToken cancellationToken)
        {
            var asyncResetEvent = new AsyncManualResetEvent(initialState: false);
            handle.OnComplete += (r) =>
            {
                asyncResetEvent.Set();
            };
            return asyncResetEvent.WaitAsync(cancellationToken);
        }

        private RazorOmniSharpRequestInvoker CreateRequestInvoker(
            TimeSpan? requestTimeout = null,
            TestRequestRouter requestRouter = null)
        {
            var options = requestTimeout == null ? Options : new RequestInvokerOptions(requestTimeout.Value, supportContentModified: false, concurrency: int.MaxValue);
            requestRouter ??= RequestRouter;
            var requestInvoker = new RazorOmniSharpRequestInvoker(
                options,
                OutputHandler,
                requestRouter,
                TestRequestProcessIdentifier.Instance,
                TestLoggerFactory.Instance);
            return requestInvoker;
        }

        private class TestOutputHandler : IOutputHandler
        {
            private readonly List<object> _sentItems = new();

            public IReadOnlyList<object> SentItems => _sentItems;

            public void Send(object value)
            {
                _sentItems.Add(value);
            }

            public Task StopAsync()
            {
                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }
        }

        private class TestRequestRouter : IRequestRouter<IHandlerDescriptor>
        {
            private readonly TimeSpan _routeDelay;
            private readonly Action _onRequestCallback;

            public event Action<Notification> OnNotificationCancelled;

            public event Action<Notification> OnNotificationCompleted;

            public TestRequestRouter(
                TimeSpan? routeDelay = null,
                Action onRequestCallback = null)
            {
                _routeDelay = routeDelay ?? TimeSpan.Zero;
                _onRequestCallback = onRequestCallback;
            }

            public IRequestDescriptor<IHandlerDescriptor> GetDescriptors(Notification notification)
            {
                throw new NotImplementedException();
            }

            public IRequestDescriptor<IHandlerDescriptor> GetDescriptors(Request request)
            {
                throw new NotImplementedException();
            }

            public async Task RouteNotification(IRequestDescriptor<IHandlerDescriptor> descriptors, Notification notification, CancellationToken cancellationToken)
            {
                try
                {
                    await Task.Delay(_routeDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    OnNotificationCancelled?.Invoke(notification);
                    throw;
                }

                OnNotificationCompleted?.Invoke(notification);
            }

            public async Task<ErrorResponse> RouteRequest(IRequestDescriptor<IHandlerDescriptor> descriptors, Request request, CancellationToken cancellationToken)
            {
                _onRequestCallback?.Invoke();

                await Task.Delay(_routeDelay, cancellationToken).ConfigureAwait(false);

                var successfulResponse = new TestSuccessfulResponse(request.Id, request);
                return new ErrorResponse(successfulResponse);
            }
        }

        private record TestSuccessfulResponse : OutgoingResponse
        {
            public TestSuccessfulResponse(object id, Request request) : base(id, result: true, request)
            {

            }
        }

        private class TestRequestProcessIdentifier : IRequestProcessIdentifier
        {
            public static readonly TestRequestProcessIdentifier Instance = new();

            private TestRequestProcessIdentifier()
            {
            }

            public RequestProcessType Identify(IHandlerDescriptor descriptor)
            {
                if (descriptor.Method.StartsWith("textDocument/did"))
                {
                    return RequestProcessType.Serial;
                }

                return RequestProcessType.Parallel;
            }
        }

        private class TestRequestDescriptor : IRequestDescriptor<IHandlerDescriptor>
        {
            public TestRequestDescriptor(string method)
            {
                Default = new TestHandlerDescriptor(method);
            }

            public IHandlerDescriptor Default { get; }

            public IEnumerator<IHandlerDescriptor> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            private class TestHandlerDescriptor : IHandlerDescriptor
            {
                public TestHandlerDescriptor(string method)
                {
                    Method = method;
                }
                public string Method { get; }

                public Type HandlerType => throw new NotImplementedException();

                public Type ImplementationType => throw new NotImplementedException();

                public Type Params => throw new NotImplementedException();

                public Type Response => throw new NotImplementedException();

                public bool HasReturnType => throw new NotImplementedException();

                public bool IsDelegatingHandler => throw new NotImplementedException();

                public IJsonRpcHandler Handler => throw new NotImplementedException();

                public RequestProcessType? RequestProcessType => throw new NotImplementedException();
            }
        }
    }
}
