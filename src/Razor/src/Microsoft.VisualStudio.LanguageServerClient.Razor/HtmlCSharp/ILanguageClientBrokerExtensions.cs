// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal static class ILanguageClientBrokerExtensions
    {
        public static async Task<TOut> RequestAsync<TIn, TOut>(
            this ILanguageClientBroker languageClientBroker,
            string[] contentTypes,
            Func<ServerCapabilities, bool> capabilitiesFilter,
            string method,
            TIn parameters,
            CancellationToken cancellationToken)
        {
            // Ideally we want to call ILanguageServiceBroker2.RequestAsync directly but it is not referenced
            // because the LanguageClient.Implementation assembly isn't published to a public feed.
            // So for now, we invoke it using reflection. This will go away eventually.
            var type = languageClientBroker.GetType();
            var requestAsyncMethod = type.GetMethod(
                "RequestAsync",
                new[]
                {
                    typeof(string[]),
                    typeof(Func<JToken, bool>),
                    typeof(string),
                    typeof(JToken),
                    typeof(CancellationToken)
                });

            var serializedParams = JToken.FromObject(parameters);
            var task = (Task<(ILanguageClient, JToken)>)requestAsyncMethod.Invoke(
                languageClientBroker,
                new object[] { contentTypes, (Func<JToken, bool>)convertedCapabilitiesFilter, method, serializedParams, cancellationToken });

            var (client, resultToken) = await task;

            var result = resultToken != null ? resultToken.ToObject<TOut>() : default;
            return result;

            bool convertedCapabilitiesFilter(JToken token)
            {
                var input = token.ToObject<ServerCapabilities>();
                return capabilitiesFilter(input);
            }
        }
    }
}
