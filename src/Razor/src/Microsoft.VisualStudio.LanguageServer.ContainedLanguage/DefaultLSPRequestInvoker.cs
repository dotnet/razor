// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

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

    [Obsolete("Will be removed in a future version.")]
    public override Task<IEnumerable<ReinvokeResponse<TOut>>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(string method, string contentType, TIn parameters, CancellationToken cancellationToken)
    {
        var capabilitiesFilter = _fallbackCapabilitiesFilterResolver.Resolve(method);
        return RequestMultipleServerCoreAsync<TIn, TOut>(method, contentType, capabilitiesFilter, parameters, cancellationToken);
    }

    [Obsolete("Will be removed in a future version.")]
    public override Task<IEnumerable<ReinvokeResponse<TOut>>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(string method, string contentType, Func<JToken, bool> capabilitiesFilter, TIn parameters, CancellationToken cancellationToken)
    {
        return RequestMultipleServerCoreAsync<TIn, TOut>(method, contentType, capabilitiesFilter, parameters, cancellationToken);
    }

    public override async Task<ReinvokeResponse<TOut>> ReinvokeRequestOnServerAsync<TIn, TOut>(
        string method,
        string languageServerName,
        TIn parameters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(method))
        {
            throw new ArgumentException("message", nameof(method));
        }

        var resultToken = await _languageServiceBroker.RequestAsync(
            new GeneralRequest<TIn, TOut> { LanguageServerName = languageServerName, Method = method, Request = parameters },
            cancellationToken);

        var result = resultToken is not null ? new ReinvokeResponse<TOut>(resultToken) : default;
        return result;
    }

    [Obsolete("Will be removed in a future version.")]
    public override Task<ReinvokeResponse<TOut>> ReinvokeRequestOnServerAsync<TIn, TOut>(
        string method,
        string languageServerName,
        Func<JToken, bool> capabilitiesFilter,
        TIn parameters,
        CancellationToken cancellationToken)
    {
        return ReinvokeRequestOnServerAsync<TIn, TOut>(method, languageServerName, parameters, cancellationToken);
    }

    public override async Task<ReinvocationResponse<TOut>?> ReinvokeRequestOnServerAsync<TIn, TOut>(ITextBuffer textBuffer, string method, string languageServerName, TIn parameters, CancellationToken cancellationToken)
    {
        var response = await _languageServiceBroker.RequestAsync(
                   new DocumentRequest<TIn, TOut> { ParameterFactory = _ => parameters, LanguageServerName = languageServerName, Method = method, TextBuffer = textBuffer },
                   cancellationToken);

        if (response is null)
        {
            return null;
        }

        var reinvocationResponse = response is not null ? new ReinvocationResponse<TOut>(response) : default;
        return reinvocationResponse;
    }

    [Obsolete("Will be removed in a future version.")]
    public override Task<ReinvocationResponse<TOut>?> ReinvokeRequestOnServerAsync<TIn, TOut>(
        ITextBuffer textBuffer,
        string method,
        string languageServerName,
        Func<JToken, bool> capabilitiesFilter,
        TIn parameters,
        CancellationToken cancellationToken)
    {
        return ReinvokeRequestOnServerAsync<TIn, TOut>(textBuffer, method, languageServerName, parameters, cancellationToken);
    }

    [Obsolete("This property is obsolete and will be removed in a future version.")]
    private async Task<IEnumerable<ReinvokeResponse<TOut>>> RequestMultipleServerCoreAsync<TIn, TOut>(string method, string contentType, Func<JToken, bool> capabilitiesFilter, TIn parameters, CancellationToken cancellationToken)
        where TIn : notnull
    {
        if (string.IsNullOrEmpty(method))
        {
            throw new ArgumentException("message", nameof(method));
        }

        var serializedParams = JToken.FromObject(parameters);

        var clientAndResultTokenPairs = await _languageServiceBroker.RequestMultipleAsync(
            new[] { contentType },
            capabilitiesFilter,
            method,
            serializedParams,
            cancellationToken).ConfigureAwait(false);

        // a little ugly - tuple deconstruction in lambda arguments doesn't work - https://github.com/dotnet/csharplang/issues/258
        var results = clientAndResultTokenPairs.Select((clientAndResultToken) => clientAndResultToken.Item2 is not null ? new ReinvokeResponse<TOut>(clientAndResultToken.Item1, clientAndResultToken.Item2.ToObject<TOut>(_serializer)!) : default);

        return results;
    }

    public override async IAsyncEnumerable<ReinvocationResponse<TOut>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(
        ITextBuffer textBuffer,
        string method,
        TIn parameters,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requests = _languageServiceBroker.RequestAllAsync(
             new DocumentRequest<TIn, TOut> { ParameterFactory = _ => parameters, Method = method, TextBuffer = textBuffer },
            cancellationToken);

        await foreach (var response in requests)
        {
            yield return new ReinvocationResponse<TOut>(response.response);
        }
    }

    [Obsolete("Will be removed in a future version.")]
    public override IAsyncEnumerable<ReinvocationResponse<TOut>> ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(
        ITextBuffer textBuffer,
        string method,
        Func<JToken, bool> capabilitiesFilter,
        TIn parameters,
        CancellationToken cancellationToken)
    {
        return ReinvokeRequestOnMultipleServersAsync<TIn, TOut>(textBuffer, method, parameters, cancellationToken);
    }
}
