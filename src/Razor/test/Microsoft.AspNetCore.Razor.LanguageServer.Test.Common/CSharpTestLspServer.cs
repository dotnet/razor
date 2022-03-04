// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Nerdbank.Streams;
using StreamJsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Common
{
    public sealed class CSharpTestLspServer : IDisposable
    {
        public readonly AdhocWorkspace TestWorkspace;
        private readonly IRazorLanguageServerTarget _languageServer;
        private readonly StreamJsonRpc.JsonRpc _clientRpc;

        private CSharpTestLspServer(
            AdhocWorkspace testWorkspace,
            ServerCapabilities serverCapabilities)
        {
            TestWorkspace = testWorkspace;

            var (clientStream, serverStream) = FullDuplexStream.CreatePair();
            _languageServer = CreateLanguageServer(serverStream, serverStream, testWorkspace, serverCapabilities);

            var messageFormatter = CreateJsonMessageFormatter();
            var messageHandler = new HeaderDelimitedMessageHandler(clientStream, clientStream, messageFormatter);
            _clientRpc = new StreamJsonRpc.JsonRpc(messageHandler)
            {
                ExceptionStrategy = ExceptionProcessing.ISerializable,
            };

            _clientRpc.StartListening();
        }

        private static JsonMessageFormatter CreateJsonMessageFormatter()
        {
            var messageFormatter = new JsonMessageFormatter();
            VSInternalExtensionUtilities.AddVSInternalExtensionConverters(messageFormatter.JsonSerializer);
            return messageFormatter;
        }

        internal static async Task<CSharpTestLspServer> CreateAsync(
            AdhocWorkspace testWorkspace,
            ClientCapabilities clientCapabilities,
            ServerCapabilities serverCapabilities)
        {
            var server = new CSharpTestLspServer(testWorkspace, serverCapabilities);

            await server.ExecuteRequestAsync<InitializeParams, InitializeResult>(Methods.InitializeName, new InitializeParams
            {
                Capabilities = clientCapabilities,
            }, CancellationToken.None);

            return server;
        }

        private static IRazorLanguageServerTarget CreateLanguageServer(
            Stream inputStream,
            Stream outputStream,
            Workspace workspace,
            ServerCapabilities serverCapabilities)
        {
            var capabilitiesProvider = new RazorCapabilitiesProvider(serverCapabilities);

            var messageHandler = new HeaderDelimitedMessageHandler(outputStream, inputStream, CreateJsonMessageFormatter());
            var jsonRpc = new StreamJsonRpc.JsonRpc(messageHandler)
            {
                ExceptionStrategy = ExceptionProcessing.ISerializable,
            };

            var exportProvider = TestCompositions.Roslyn.ExportProviderFactory.CreateExportProvider();
            var registrationService = exportProvider.GetExportedValue<RazorTestWorkspaceRegistrationService>();
            registrationService.Register(workspace);

            var languageServerFactory = exportProvider.GetExportedValue<IRazorLanguageServerFactoryWrapper>();
            var languageServer = languageServerFactory.CreateLanguageServer(jsonRpc, capabilitiesProvider);

            jsonRpc.StartListening();
            return languageServer;
        }

        public async Task<ResponseType?> ExecuteRequestAsync<RequestType, ResponseType>(string methodName, RequestType request, CancellationToken cancellationToken) where RequestType : class
        {
            var result = await _clientRpc.InvokeWithParameterObjectAsync<ResponseType>(methodName, request, cancellationToken: cancellationToken).ConfigureAwait(false);
            return result;
        }

        public Solution GetCurrentSolution() => TestWorkspace.CurrentSolution;

        public void Dispose()
        {
            TestWorkspace.Dispose();
            _clientRpc.Dispose();
        }

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
