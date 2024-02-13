// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal class TestLanguageServiceBroker : ILanguageServiceBroker2
{
    private readonly Action<string> _callback;

#pragma warning disable CS0067 // The event is never used
    public event EventHandler<LanguageClientLoadedEventArgs> LanguageClientLoaded;
    public event AsyncEventHandler<LanguageClientNotifyEventArgs> ClientNotifyAsync;
#pragma warning restore CS0067 // The event is never used

    public IEnumerable<ILanguageClientInstance> ActiveLanguageClients => throw new NotImplementedException();

    public IEnumerable<Lazy<ILanguageClient, IContentTypeMetadata>> FactoryLanguageClients => throw new NotImplementedException();

    public IEnumerable<Lazy<ILanguageClient, IContentTypeMetadata>> LanguageClients => throw new NotImplementedException();

    public TestLanguageServiceBroker(Action<string> callback)
    {
        _callback = callback;
    }

    public Task LoadAsync(ILanguageClientMetadata metadata, ILanguageClient client)
    {
        throw new NotImplementedException();
    }

    public Task<(ILanguageClient, JToken)> RequestAsync(
        string[] contentTypes,
        Func<JToken, bool> capabilitiesFilter,
        string method,
        JToken parameters,
        CancellationToken cancellationToken)
    {
        _callback?.Invoke(method);

        return Task.FromResult<(ILanguageClient, JToken)>((null, null));
    }

    public Task<(ILanguageClient, JToken)> RequestAsync(string[] contentTypes, Func<JToken, bool> capabilitiesFilter, string clientName, string method, JToken parameters, CancellationToken cancellationToken)
    {
        _callback?.Invoke(method);

        return Task.FromResult<(ILanguageClient, JToken)>((null, null));
    }

    public IEnumerable<(Uri, JToken)> GetAllDiagnostics()
    {
        throw new NotImplementedException();
    }

    public JToken GetDiagnostics(Uri uri)
    {
        throw new NotImplementedException();
    }

    public Task<JToken> RequestAsync(ILanguageClient languageClient, string method, JToken parameters, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<(ILanguageClient, JToken)>> RequestMultipleAsync(string[] contentTypes, Func<JToken, bool> capabilitiesFilter, string method, JToken parameters, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void AddCustomBufferContentTypes(IEnumerable<string> contentTypes)
    {
        throw new NotImplementedException();
    }

    public void RemoveCustomBufferContentTypes(IEnumerable<string> contentTypes)
    {
        throw new NotImplementedException();
    }

    public void AddLanguageClients(IEnumerable<Lazy<ILanguageClient, IContentTypeMetadata>> items)
    {
        throw new NotImplementedException();
    }

    public void RemoveLanguageClients(IEnumerable<Lazy<ILanguageClient, IContentTypeMetadata>> items)
    {
        throw new NotImplementedException();
    }

    public Task LoadAsync(IContentTypeMetadata contentType, ILanguageClient client)
    {
        throw new NotImplementedException();
    }

    public Task OnDidOpenTextDocumentAsync(ITextSnapshot snapShot)
    {
        throw new NotImplementedException();
    }

    public Task OnDidCloseTextDocumentAsync(ITextSnapshot snapShot)
    {
        throw new NotImplementedException();
    }

    public Task OnDidChangeTextDocumentAsync(ITextSnapshot before, ITextSnapshot after, IEnumerable<ITextChange> textChanges)
    {
        throw new NotImplementedException();
    }

    public Task OnDidSaveTextDocumentAsync(ITextDocument document)
    {
        throw new NotImplementedException();
    }

    public Task<(ILanguageClient, TOut)> RequestAsync<TIn, TOut>(string[] contentTypes, Func<ServerCapabilities, bool> capabilitiesFilter, LspRequest<TIn, TOut> method, TIn parameters, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TOut> RequestAsync<TIn, TOut>(ILanguageClient languageClient, LspRequest<TIn, TOut> method, TIn parameters, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<(ILanguageClient, TOut)>> RequestMultipleAsync<TIn, TOut>(string[] contentTypes, Func<ServerCapabilities, bool> capabilitiesFilter, LspRequest<TIn, TOut> method, TIn parameters, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ManualInvocationResponse> RequestAsync(ITextBuffer textBuffer, Func<JToken, bool> capabilitiesFilter, string languageServerName, string method, Func<ITextSnapshot, JToken> parameterFactory, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<ManualInvocationResponse> RequestMultipleAsync(ITextBuffer textBuffer, Func<JToken, bool> capabilitiesFilter, string method, Func<ITextSnapshot, JToken> parameterFactory, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task NotifyAsync(ILanguageClient languageClient, string method, JToken parameters, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Notify<T>(Notification<T> notification, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TResponse> RequestAsync<TRequest, TResponse>(Request<TRequest, TResponse> request, CancellationToken cancellationToken)
    {
        _callback?.Invoke(request.Method);

        return Task.FromResult((TResponse)(object)null);
    }

    public IAsyncEnumerable<(string client, TResponse response)> RequestMultipleAsync<TRequest, TResponse>(Request<TRequest, TResponse> request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
