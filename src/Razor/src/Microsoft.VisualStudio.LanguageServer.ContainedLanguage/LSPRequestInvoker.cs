// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal abstract class LSPRequestInvoker
    {
        public abstract Task<ReinvokeResponse<TOut>> ReinvokeRequestOnServerAsync<TIn, TOut>(
            string method,
            string languageServerName,
            TIn parameters,
            CancellationToken cancellationToken)
            where TIn : notnull;

        public abstract Task<ReinvokeResponse<TOut>> ReinvokeRequestOnServerAsync<TIn, TOut>(
            string method,
            string languageServerName,
            Func<JToken, bool> capabilitiesFilter,
            TIn parameters,
            CancellationToken cancellationToken)
            where TIn : notnull;

        public abstract Task<ReinvocationResponse<TOut>?> ReinvokeRequestOnServerAsync<TIn, TOut>(
            ITextBuffer textBuffer,
            string method,
            string languageServerName,
            TIn parameters,
            CancellationToken cancellationToken)
            where TIn : notnull;

        public abstract Task<ReinvocationResponse<TOut>?> ReinvokeRequestOnServerAsync<TIn, TOut>(
            ITextBuffer textBuffer,
            string method,
            string languageServerName,
            Func<JToken, bool> capabilitiesFilter,
            TIn parameters,
            CancellationToken cancellationToken)
            where TIn : notnull;

        public abstract Task<IEnumerable<ReinvokeResponse<TOut>>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(
            string method,
            string contentType,
            TIn parameters,
            CancellationToken cancellationToken)
            where TIn : notnull;

        public abstract Task<IEnumerable<ReinvokeResponse<TOut>>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(
            string method,
            string contentType,
            Func<JToken, bool> capabilitiesFilter,
            TIn parameters,
            CancellationToken cancellationToken)
            where TIn : notnull;

        public abstract IAsyncEnumerable<ReinvocationResponse<TOut>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(
            ITextBuffer textBuffer,
            string method,
            TIn parameters,
            CancellationToken cancellationToken)
            where TIn : notnull;

        public abstract IAsyncEnumerable<ReinvocationResponse<TOut>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(
            ITextBuffer textBuffer,
            string method,
            Func<JToken, bool> capabilitiesFilter,
            TIn parameters,
            CancellationToken cancellationToken)
            where TIn : notnull;
    }
}
