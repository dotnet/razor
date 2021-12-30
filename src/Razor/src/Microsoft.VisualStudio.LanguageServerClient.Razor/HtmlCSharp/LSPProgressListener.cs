﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal abstract class LSPProgressListener
    {
        public abstract bool TryListenForProgress(
            string token,
            Func<JToken, CancellationToken, Task> onProgressNotifyAsync,
            Func<CancellationToken, Task> delayAfterLastNotifyAsync,
            CancellationToken handlerCancellationToken,
            [NotNullWhen(true)] out Task? onCompleted);
    }
}
