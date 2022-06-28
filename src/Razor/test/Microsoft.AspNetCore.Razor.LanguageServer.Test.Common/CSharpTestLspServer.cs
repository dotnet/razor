// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Nerdbank.Streams;
using StreamJsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Common
{
    public sealed class CSharpTestLspServer : IAsyncDisposable
    {
        private readonly AdhocWorkspace _testWorkspace;
        private readonly IRazorLanguageServerTarget _languageServer;

        private readonly StreamJsonRpc.JsonRpc _clientRpc;
        private readonly StreamJsonRpc.JsonRpc _serverRpc;

        private readonly JsonMessageFormatter _clientMessageFormatter;
        private readonly JsonMessageFormatter _serverMessageFormatter;

        private readonly HeaderDelimitedMessageHandler _clientMessageHandler;
        private readonly HeaderDelimitedMessageHandler _serverMessageHandler;

        private CSharpTestLspServer(
            AdhocWorkspace testWorkspace,
            ExportProvider exportProvider,
            ServerCapabilities serverCapabilities)
        {
            _testWorkspace = testWorkspace;

            var (clientStream, serverStream) = FullDuplexStream.CreatePair();

            _serverMessageFormatter = CreateJsonMessageFormatter();
            _serverMessageHandler = new HeaderDelimitedMessageHandler(serverStream, serverStream, _serverMessageFormatter);
            _serverRpc = new StreamJsonRpc.JsonRpc(_serverMessageHandler)
            {
                ExceptionStrategy = ExceptionProcessing.ISerializable,
            };

            _languageServer = CreateLanguageServer(_serverRpc, testWorkspace, exportProvider, serverCapabilities);

            _clientMessageFormatter = CreateJsonMessageFormatter();
            _clientMessageHandler = new HeaderDelimitedMessageHandler(clientStream, clientStream, _clientMessageFormatter);
            _clientRpc = new StreamJsonRpc.JsonRpc(_clientMessageHandler)
            {
                ExceptionStrategy = ExceptionProcessing.ISerializable,
            };

            _clientRpc.StartListening();

            static JsonMessageFormatter CreateJsonMessageFormatter()
            {
                var messageFormatter = new JsonMessageFormatter();
                VSInternalExtensionUtilities.AddVSInternalExtensionConverters(messageFormatter.JsonSerializer);
                return messageFormatter;
            }

            static IRazorLanguageServerTarget CreateLanguageServer(
                StreamJsonRpc.JsonRpc serverRpc,
                Workspace workspace,
                ExportProvider exportProvider,
                ServerCapabilities serverCapabilities)
            {
                var capabilitiesProvider = new RazorCapabilitiesProvider(serverCapabilities);

                var registrationService = exportProvider.GetExportedValue<RazorTestWorkspaceRegistrationService>();
                registrationService.Register(workspace);

                var languageServerFactory = exportProvider.GetExportedValue<IRazorLanguageServerFactoryWrapper>();
                var languageServer = languageServerFactory.CreateLanguageServer(serverRpc, capabilitiesProvider);

                serverRpc.StartListening();
                return languageServer;
            }
        }

        internal static async Task<CSharpTestLspServer> CreateAsync(
            AdhocWorkspace testWorkspace,
            ExportProvider exportProvider,
            ClientCapabilities clientCapabilities,
            ServerCapabilities serverCapabilities)
        {
            var server = new CSharpTestLspServer(testWorkspace, exportProvider, serverCapabilities);

            await server.ExecuteRequestAsync<InitializeParams, InitializeResult>(Methods.InitializeName, new InitializeParams
            {
                Capabilities = clientCapabilities,
            }, CancellationToken.None);

            await server.ExecuteRequestAsync(Methods.InitializedName, new InitializedParams(), CancellationToken.None);
            return server;
        }

        internal Task ExecuteRequestAsync<RequestType>(
            string methodName,
            RequestType request,
            CancellationToken cancellationToken) where RequestType : class
            => _clientRpc.InvokeWithParameterObjectAsync(
                methodName,
                request,
                cancellationToken);

        internal Task<ResponseType?> ExecuteRequestAsync<RequestType, ResponseType>(
            string methodName,
            RequestType request,
            CancellationToken cancellationToken)
            => _clientRpc.InvokeWithParameterObjectAsync<ResponseType?>(
                methodName,
                request,
                cancellationToken);

        public async ValueTask DisposeAsync()
        {
            _testWorkspace.Dispose();
            await _languageServer.DisposeAsync();

            _clientRpc.Dispose();
            _clientMessageFormatter.Dispose();
            await _clientMessageHandler.DisposeAsync();

            _serverRpc.Dispose();
            _serverMessageFormatter.Dispose();
            await _serverMessageHandler.DisposeAsync();
        }

        #region Document Change Methods

        public async Task OpenDocumentAsync(Uri documentUri, string documentText)
        {
            var didOpenParams = CreateDidOpenTextDocumentParams(documentUri, documentText);
            await ExecuteRequestAsync<DidOpenTextDocumentParams, object>(Methods.TextDocumentDidOpenName, didOpenParams, CancellationToken.None).ConfigureAwait(false);

            static DidOpenTextDocumentParams CreateDidOpenTextDocumentParams(Uri uri, string source)
                => new()
                {
                    TextDocument = new TextDocumentItem
                    {
                        Text = source,
                        Uri = uri
                    }
                };
        }

        public async Task ReplaceTextAsync(Uri documentUri, params (Range Range, string Text)[] changes)
        {
            var didChangeParams = CreateDidChangeTextDocumentParams(
                documentUri,
                changes.Select(change => (change.Range, change.Text)).ToImmutableArray());
            await ExecuteRequestAsync<DidChangeTextDocumentParams, object>(Methods.TextDocumentDidChangeName, didChangeParams, CancellationToken.None).ConfigureAwait(false);

            static DidChangeTextDocumentParams CreateDidChangeTextDocumentParams(Uri documentUri, ImmutableArray<(Range Range, string Text)> changes)
            {
                var changeEvents = changes.Select(change => new TextDocumentContentChangeEvent
                {
                    Text = change.Text,
                    Range = change.Range,
                }).ToArray();

                return new DidChangeTextDocumentParams()
                {
                    TextDocument = new VersionedTextDocumentIdentifier
                    {
                        Uri = documentUri
                    },
                    ContentChanges = changeEvents
                };
            }
        }

        #endregion

        private class RazorCapabilitiesProvider : IRazorCapabilitiesProvider
        {
            private readonly ServerCapabilities _serverCapabilities;

            public RazorCapabilitiesProvider(ServerCapabilities serverCapabilities)
            {
                _serverCapabilities = serverCapabilities;
            }

            public ServerCapabilities GetCapabilities(ClientCapabilities clientCapabilities) => _serverCapabilities;
        }
    }
}
