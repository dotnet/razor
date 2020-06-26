// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal class CallbackRequest
    {
        public CallbackRequest(Func<JToken, Task> callback)
        {
            if (callback is null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            Callback = callback;
        }

        public CallbackRequest(
            Func<JToken, Task> callback,
            TimeSpan timeoutAfterLastNotify,
            EventWaitHandle waitForEventCompletion) : this(callback)
        {
            if (timeoutAfterLastNotify == default)
            {
                throw new ArgumentNullException(nameof(timeoutAfterLastNotify));
            }

            if (waitForEventCompletion is null)
            {
                throw new ArgumentNullException(nameof(waitForEventCompletion));
            }

            TimeoutAfterLastNotify = timeoutAfterLastNotify;
            WaitForEventCompletion = waitForEventCompletion;
        }

        public Func<JToken, Task> Callback { get; }
        public EventWaitHandle WaitForEventCompletion { get; }

        public TimeSpan TimeoutAfterLastNotify { get; }
        public object RequestLock { get; } = new object();
        public CancellationTokenSource CancellationTokenSource { get; set; }
    }
}
