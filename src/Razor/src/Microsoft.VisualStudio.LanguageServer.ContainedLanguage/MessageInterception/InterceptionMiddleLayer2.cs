// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage.MessageInterception;

public class InterceptionMiddleLayer2 : ILanguageClientMiddleLayer2
{
    private readonly InterceptorManager _interceptorManager;
    private readonly string _contentType;

    /// <summary>
    /// Create the middle layer
    /// </summary>
    /// <param name="interceptorManager">Interception manager</param>
    /// <param name="contentType">The content type name of the language for the ILanguageClient using this middle layer</param>
    public InterceptionMiddleLayer2(InterceptorManager interceptorManager, string contentType)
    {
        _interceptorManager = interceptorManager ?? throw new ArgumentNullException(nameof(interceptorManager));
        _contentType = !string.IsNullOrEmpty(contentType) ? contentType : throw new ArgumentException("Cannot be empty", nameof(contentType));
    }

    public bool CanHandle(string methodName)
    {
        return _interceptorManager.HasInterceptor(methodName, _contentType);
    }

    public async Task HandleNotificationAsync<TMessage>(string methodName, TMessage message, Func<TMessage, Task> sendNotification)
    {
        var payload = message;
        if (CanHandle(methodName))
        {
            payload = await _interceptorManager.ProcessInterceptorsAsync(methodName, payload, _contentType, CancellationToken.None);
        }

        if (payload is not null)
        {
            // this completes the handshake to give the payload back to the client.
            await sendNotification(payload);
        }
    }

    public async Task<TResponse?> HandleRequestAsync<TRequest, TResponse>(string methodName, TRequest methodParam, Func<TRequest, Task<TResponse?>> sendRequest)
    {
        // First send the request through.
        // We don't yet have a scenario where the request needs to be intercepted, but if one does come up, we may need to redesign the interception handshake
        // to handle both request and response interception.
        var response = await sendRequest(methodParam);

        if (response is not null && CanHandle(methodName))
        {
            response = await _interceptorManager.ProcessInterceptorsAsync(methodName, response, _contentType, CancellationToken.None);
        }

        return response;
    }
}
