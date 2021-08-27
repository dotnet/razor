// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.JsonRpc.Server.Messages;
using static Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc.JsonRpcRequestScheduler;

namespace Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc
{
    internal class RazorOmniSharpRequestInvoker : RequestInvoker
    {
        private readonly TimeSpan _requestTimeout;
        private readonly IOutputHandler _outputHandler;
        private readonly IRequestRouter<IHandlerDescriptor?> _requestRouter;
        private readonly IRequestProcessIdentifier _requestProcessIdentifier;
        private readonly JsonRpcRequestScheduler _requestScheduler;
        private readonly ILogger<RazorOmniSharpRequestInvoker> _logger;

        public RazorOmniSharpRequestInvoker(
            RequestInvokerOptions options,
            IOutputHandler outputHandler,
            IRequestRouter<IHandlerDescriptor?> requestRouter,
            IRequestProcessIdentifier requestProcessIdentifier,
            ILoggerFactory loggerFactory)
        {
            _requestTimeout = options.RequestTimeout;
            _outputHandler = outputHandler;
            _requestRouter = requestRouter;
            _requestProcessIdentifier = requestProcessIdentifier;
            _requestScheduler = new JsonRpcRequestScheduler(loggerFactory);
            _logger = loggerFactory.CreateLogger<RazorOmniSharpRequestInvoker>();
        }

        public override RequestInvocationHandle InvokeRequest(IRequestDescriptor<IHandlerDescriptor?> descriptor, Request request)
        {
            if (descriptor.Default is null)
            {
                throw new ArgumentNullException(nameof(descriptor.Default));
            }

            var handle = new RequestInvocationHandle(request);
            var type = _requestProcessIdentifier.Identify(descriptor.Default);

            var schedulerDelegate = BuildRequestDelegate(descriptor, request, handle);
            _requestScheduler.Schedule(type, $"{request.Method}:{request.Id}", schedulerDelegate);

            return handle;
        }

        public override void InvokeNotification(IRequestDescriptor<IHandlerDescriptor?> descriptor, Notification notification)
        {
            if (descriptor.Default is null)
            {
                throw new ArgumentNullException(nameof(descriptor.Default));
            }

            var type = _requestProcessIdentifier.Identify(descriptor.Default);
            var schedulerDelegate = BuildNotificationDelegate(descriptor, notification);
            _requestScheduler.Schedule(type, notification.Method, schedulerDelegate);
        }

        public override void Dispose()
        {
            _requestScheduler.Dispose();
        }

        private ProcessSchedulerDelegate BuildRequestDelegate(IRequestDescriptor<IHandlerDescriptor?> descriptors, Request request, RequestInvocationHandle handle)
        {
            return async (cancellationToken) =>
            {
                try
                {
                    var result = await InvokeRequestAsync(cancellationToken).ConfigureAwait(false);
                    _outputHandler.Send(result.Value);
                }
                finally
                {
                    handle.Dispose();
                }
            };

            async Task<ErrorResponse> InvokeRequestAsync(CancellationToken cancellationToken)
            {
                var timeoutCts = new CancellationTokenSource(_requestTimeout);
                var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(handle.CancellationTokenSource.Token, timeoutCts.Token, cancellationToken);

                using var timer = _logger.TimeDebug("Processing request {Method}:{Id}", request.Method, request.Id);
                try
                {
                    var result = await _requestRouter.RouteRequest(descriptors, request, combinedCts.Token).ConfigureAwait(false);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    if (timeoutCts.IsCancellationRequested)
                    {
                        _logger.LogTrace("Request {Method}:{Id} was cancelled, due to timeout", request.Method, request.Id);
                        return new RequestCancelled(request.Id, request.Method);
                    }

                    _logger.LogTrace("Request {Method}:{Id} was cancelled", request.Method, request.Id);
                    return new RequestCancelled(request.Id, request.Method);
                }
                catch (RpcErrorException e)
                {
                    _logger.LogCritical(Events.UnhandledRequest, e, "Failed to handle request {Method}:{Id}", request.Method, request.Id);
                    return new RpcError(
                        request.Id,
                        request.Method,
                        new ErrorMessage(e.Code, e.Message, e.Error));
                }
                catch (Exception e)
                {
                    _logger.LogCritical(Events.UnhandledRequest, e, "Failed to handle request {Method}:{Id}. Unhandled exception", request.Method, request.Id);
                    return new InternalError(request.Id, request.Method, e.ToString());
                }
            }
        }

        private ProcessSchedulerDelegate BuildNotificationDelegate(IRequestDescriptor<IHandlerDescriptor?> descriptors, Notification notification)
        {
            return async (cancellationToken) =>
            {
                var timeoutCts = new CancellationTokenSource(_requestTimeout);
                var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                using var timer = _logger.TimeDebug("Processing notification {Method}", notification.Method);
                try
                {
                    await _requestRouter.RouteNotification(descriptors, notification, combinedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (timeoutCts.IsCancellationRequested)
                    {
                        _logger.LogTrace("Notification {Method} was cancelled due to timeout", notification.Method);
                        return;
                    }

                    _logger.LogTrace("Notification {Method} was cancelled", notification.Method);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(Events.UnhandledRequest, e, "Failed to handle notification {Method}", notification.Method);
                }
            };
        }
    }
}
