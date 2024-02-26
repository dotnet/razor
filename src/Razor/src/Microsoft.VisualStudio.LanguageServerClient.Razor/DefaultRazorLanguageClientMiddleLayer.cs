// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

[Shared]
[Export(typeof(RazorLanguageClientMiddleLayer))]
internal class DefaultRazorLanguageClientMiddleLayer : RazorLanguageClientMiddleLayer
{
    public override bool CanHandle(string methodName) => false;

    public override Task HandleNotificationAsync<TMessage>(string methodName, TMessage message, Func<TMessage, Task> sendNotification)
    {
        return Task.CompletedTask;
    }

    public override Task<TResponse?> HandleRequestAsync<TRequest, TResponse>(string methodName, TRequest methodParam, Func<TRequest, Task<TResponse?>> sendRequest) where TResponse : default
    {
        throw new NotImplementedException();
    }
}
