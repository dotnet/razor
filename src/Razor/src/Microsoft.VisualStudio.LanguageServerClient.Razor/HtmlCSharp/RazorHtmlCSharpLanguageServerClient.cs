﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Nerdbank.Streams;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Export(typeof(ILanguageClient))]
    [ContentType(RazorLSPConstants.RazorLSPContentTypeName)]
    internal class RazorHtmlCSharpLanguageServerClient : ILanguageClient, IDisposable
    {
        private readonly IEnumerable<Lazy<IRequestHandler, IRequestHandlerMetadata>> _requestHandlers;
        private readonly HTMLCSharpLanguageServerLogHubLoggerProvider _loggerProvider;
        private RazorHtmlCSharpLanguageServer? _languageServer;

        private RazorHtmlCSharpLanguageServer LanguageServer
        {
            get
            {
                if (_languageServer is null)
                {
                    throw new InvalidOperationException($"{nameof(LanguageServer)} called before it's initialized");
                }

                return _languageServer;
            }
            set
            {
                _languageServer = value;
            }
        }

        [ImportingConstructor]
        public RazorHtmlCSharpLanguageServerClient(
            [ImportMany] IEnumerable<Lazy<IRequestHandler, IRequestHandlerMetadata>> requestHandlers!!,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider!!)
        {
            _requestHandlers = requestHandlers;
            _loggerProvider = loggerProvider;
        }

        public string Name => "Razor Html & CSharp Language Server Client";

        public IEnumerable<string>? ConfigurationSections => null;

        public object? InitializationOptions => null;

        public IEnumerable<string>? FilesToWatch => null;

        public bool ShowNotificationOnInitializeFailed => true;

        public event AsyncEventHandler<EventArgs>? StartAsync;

        public event AsyncEventHandler<EventArgs>? StopAsync
        {
            add { }
            remove { }
        }

        public async Task<Connection?> ActivateAsync(CancellationToken token)
        {
            var (clientStream, serverStream) = FullDuplexStream.CreatePair();

            LanguageServer = await RazorHtmlCSharpLanguageServer.CreateAsync(serverStream, serverStream, _requestHandlers, _loggerProvider, token);

            var connection = new Connection(clientStream, clientStream);
            return connection;
        }

        public Task OnLoadedAsync()
        {
            return StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            LanguageServer.Dispose();
        }

        public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            var initializationFailureContext = new InitializationFailureContext
            {
                FailureMessage = string.Format(VS.LSClientRazor.Resources.LanguageServer_Initialization_Failed,
                    Name, initializationState.StatusMessage, initializationState.InitializationException?.ToString())
            };
            return Task.FromResult<InitializationFailureContext?>(initializationFailureContext);
        }
    }
}
