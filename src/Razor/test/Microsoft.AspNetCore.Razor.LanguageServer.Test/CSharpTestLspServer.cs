// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Nerdbank.Streams;
using StreamJsonRpc;
using System.IO;
using System.Threading;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test
{
    public sealed class CSharpTestLspServer : IDisposable
    {
        public readonly AdhocWorkspace TestWorkspace;
        private readonly IRazorLanguageServerTarget _languageServer;
        private readonly StreamJsonRpc.JsonRpc _clientRpc;

        public LSP.ClientCapabilities ClientCapabilities { get; }

        private CSharpTestLspServer(
            AdhocWorkspace testWorkspace,
            LSP.ClientCapabilities clientCapabilities)
        {
            TestWorkspace = testWorkspace;
            ClientCapabilities = clientCapabilities;

            var (clientStream, serverStream) = FullDuplexStream.CreatePair();
            _languageServer = CreateLanguageServer(serverStream, serverStream);

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
            LSP.VSInternalExtensionUtilities.AddVSInternalExtensionConverters(messageFormatter.JsonSerializer);
            return messageFormatter;
        }

        internal static async Task<CSharpTestLspServer> CreateAsync(AdhocWorkspace testWorkspace, LSP.ClientCapabilities clientCapabilities)
        {
            var server = new CSharpTestLspServer(testWorkspace, clientCapabilities);

            try
            {
                await server.ExecuteRequestAsync<LSP.InitializeParams, LSP.InitializeResult>(LSP.Methods.InitializeName, new LSP.InitializeParams
                {
                    Capabilities = clientCapabilities,
                }, CancellationToken.None);
            }
            catch (Exception e)
            {

            }

            return server;
        }

        private static IRazorLanguageServerTarget CreateLanguageServer(Stream inputStream, Stream outputStream)
        {
            var capabilitiesProvider = new RazorCapabilitiesProvider();

            var messageHandler = new HeaderDelimitedMessageHandler(outputStream, inputStream, CreateJsonMessageFormatter());
            var jsonRpc = new StreamJsonRpc.JsonRpc(messageHandler)
            {
                ExceptionStrategy = ExceptionProcessing.ISerializable,
            };

            var exportProvider = TestCompositions.Roslyn.ExportProviderFactory.CreateExportProvider();
            var languageServerFactory = exportProvider.GetExportedValue<IRazorLanguageServerFactoryWrapper>();
            var languageServer = languageServerFactory.Create(jsonRpc, capabilitiesProvider);

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
            public ServerCapabilities GetCapabilities(ClientCapabilities clientCapabilities)
            {
                var capabilities = new ServerCapabilities
                {
                    SemanticTokensOptions = new SemanticTokensOptions
                    {
                        Full = false,
                        Range = true,
                        Legend = new SemanticTokensLegend
                        {
                            TokenTypes = RazorSemanticTokensLegend.TokenTypes.Select(t => t.ToString()).ToArray(),
                            TokenModifiers = new string[] { SemanticTokenModifiers.Static }
                        }
                    }
                };

                return capabilities;
            }
        }
    }
}
