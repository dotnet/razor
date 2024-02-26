// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal abstract class RazorLanguageClientMiddleLayer : ILanguageClientMiddleLayer2
{
    public abstract bool CanHandle(string methodName);

    public abstract Task HandleNotificationAsync<TMessage>(string methodName, TMessage message, Func<TMessage, Task> sendNotification);

    public abstract Task<TResponse?> HandleRequestAsync<TRequest, TResponse>(string methodName, TRequest methodParam, Func<TRequest, Task<TResponse?>> sendRequest);
}
