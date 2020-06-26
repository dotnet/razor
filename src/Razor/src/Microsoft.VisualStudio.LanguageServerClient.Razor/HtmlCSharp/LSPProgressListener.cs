// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal abstract class LSPProgressListener
    {
        internal abstract bool TryListenForProgress(
            string requestId,
            Func<JToken, Task> onProgressResult,
            TimeSpan timeoutAfterLastNotify,
            out Task onCompleted);
    }
}
