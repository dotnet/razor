// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    [Shared]
    [Export(typeof(LSPRequestInvoker))]
    internal class DefaultLSPRequestInvoker : LSPRequestInvoker
    {
        private readonly ILanguageServiceBroker2 _languageServiceBroker;
        private readonly FallbackCapabilitiesFilterResolver _fallbackCapabilitiesFilterResolver;
        private readonly JsonSerializer _serializer;

        [ImportingConstructor]
        public DefaultLSPRequestInvoker(
            ILanguageServiceBroker2 languageServiceBroker,
            FallbackCapabilitiesFilterResolver fallbackCapabilitiesFilterResolver)
        {
            if (languageServiceBroker is null)
            {
                throw new ArgumentNullException(nameof(languageServiceBroker));
            }

            if (fallbackCapabilitiesFilterResolver is null)
            {
                throw new ArgumentNullException(nameof(fallbackCapabilitiesFilterResolver));
            }

            _languageServiceBroker = languageServiceBroker;
            _fallbackCapabilitiesFilterResolver = fallbackCapabilitiesFilterResolver;

            // We need these converters so we don't lose information as part of the deserialization.
            _serializer = new JsonSerializer();
            _serializer.AddVSInternalExtensionConverters();
        }

        public override Task<IEnumerable<ReinvokeResponse<TOut>>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(string method, string contentType, TIn parameters, CancellationToken cancellationToken)
        {
            var capabilitiesFilter = _fallbackCapabilitiesFilterResolver.Resolve(method);
            return RequestMultipleServerCoreAsync<TIn, TOut>(method, contentType, capabilitiesFilter, parameters, cancellationToken);
        }

        public override Task<IEnumerable<ReinvokeResponse<TOut>>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(string method, string contentType, Func<JToken, bool> capabilitiesFilter, TIn parameters, CancellationToken cancellationToken)
        {
            return RequestMultipleServerCoreAsync<TIn, TOut>(method, contentType, capabilitiesFilter, parameters, cancellationToken);
        }

        public override Task<ReinvokeResponse<TOut>> ReinvokeRequestOnServerAsync<TIn, TOut>(
            string method,
            string languageServerName,
            TIn parameters,
            CancellationToken cancellationToken)
        {
            return ReinvokeRequestOnServerAsync<TIn, TOut>(method, languageServerName, capabilitiesFilter: null, parameters, cancellationToken);
        }

        public override async Task<ReinvokeResponse<TOut>> ReinvokeRequestOnServerAsync<TIn, TOut>(
            string method,
            string languageServerName,
            Func<JToken, bool> capabilitiesFilter,
            TIn parameters,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(method))
            {
                throw new ArgumentException("message", nameof(method));
            }

            if (capabilitiesFilter is null)
            {
                capabilitiesFilter = _fallbackCapabilitiesFilterResolver.Resolve(method);
            }

            var serializedParams = JToken.FromObject(parameters);

            var (languageClient, resultToken) = await _languageServiceBroker.RequestAsync(
                Array.Empty<string>(),
                capabilitiesFilter,
                languageServerName,
                method,
                serializedParams,
                cancellationToken);

            var result = resultToken != null ? new ReinvokeResponse<TOut>(languageClient, resultToken.ToObject<TOut>(_serializer)) : default;
            return result;
        }

        private async Task<IEnumerable<ReinvokeResponse<TOut>>> RequestMultipleServerCoreAsync<TIn, TOut>(string method, string contentType, Func<JToken, bool> capabilitiesFilter, TIn parameters, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(method))
            {
                throw new ArgumentException("message", nameof(method));
            }

            var serializedParams = JToken.FromObject(parameters);

#pragma warning disable CS0618 // Type or member is obsolete
            var clientAndResultTokenPairs = await _languageServiceBroker.RequestMultipleAsync(
                new[] { contentType },
                capabilitiesFilter,
                method,
                serializedParams,
                cancellationToken).ConfigureAwait(false);
#pragma warning restore CS0618 // Type or member is obsolete

            // a little ugly - tuple deconstruction in lambda arguments doesn't work - https://github.com/dotnet/csharplang/issues/258
            var results = clientAndResultTokenPairs.Select((clientAndResultToken) => clientAndResultToken.Item2 != null ? new ReinvokeResponse<TOut>(clientAndResultToken.Item1, clientAndResultToken.Item2.ToObject<TOut>(_serializer)) : default);

            return results;
        }
    }
}
