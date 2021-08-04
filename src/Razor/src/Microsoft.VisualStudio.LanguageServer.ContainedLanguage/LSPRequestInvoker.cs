// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal abstract class LSPRequestInvoker
    {
        public abstract Task<ReinvokeResponse<TOut>> ReinvokeRequestOnServerAsync<TIn, TOut>(
            string method,
            string languageServerName,
            TIn parameters,
            CancellationToken cancellationToken);

        public abstract Task<ReinvokeResponse<TOut>> ReinvokeRequestOnServerAsync<TIn, TOut>(
            string method,
            string languageServerName,
            Func<JToken, bool> capabilitiesFilter,
            TIn parameters,
            CancellationToken cancellationToken);

        public abstract Task<IEnumerable<ReinvokeResponse<TOut>>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(
            string method,
            string contentType,
            TIn parameters,
            CancellationToken cancellationToken);

        public abstract Task<IEnumerable<ReinvokeResponse<TOut>>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(
            string method,
            string contentType,
            Func<JToken, bool> capabilitiesFilter,
            TIn parameters,
            CancellationToken cancellationToken);
    }
}
