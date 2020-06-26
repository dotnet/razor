// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal abstract class LSPProgressListener
    {
        public abstract Task ClientNotifyAsyncListenerAsync(object sender, LanguageClientNotifyEventArgs args);

        internal abstract bool Subscribe(CallbackRequest callbackRequest, string requestId);

        internal abstract bool Unsubscribe(string requestId);
    }
}
