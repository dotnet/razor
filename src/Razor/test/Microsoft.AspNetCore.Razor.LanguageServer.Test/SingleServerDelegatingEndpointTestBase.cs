// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Xunit;
using DefinitionResult = Microsoft.VisualStudio.LanguageServer.Protocol.SumType<
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalLocation,
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalLocation[],
    Microsoft.VisualStudio.LanguageServer.Protocol.DocumentLink[]>;
using ImplementationResult = Microsoft.VisualStudio.LanguageServer.Protocol.SumType<
    Microsoft.VisualStudio.LanguageServer.Protocol.Location[],
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalReferenceItem[]>;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    [UseExportProvider]
    public abstract class SingleServerDelegatingEndpointTestBase : LanguageServerTestBase
    {
        internal DocumentContextFactory DocumentContextFactory { get; private set; }
        internal LanguageServerFeatureOptions LanguageServerFeatureOptions { get; private set; }
        internal ClientNotifierServiceBase LanguageServer { get; private set; }
        internal RazorDocumentMappingService DocumentMappingService { get; private set; }

        protected async Task CreateLanguageServerAsync(RazorCodeDocument codeDocument, string razorFilePath)
        {
            var realLanguageServerFeatureOptions = new DefaultLanguageServerFeatureOptions();

            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var csharpDocumentUri = new Uri(realLanguageServerFeatureOptions.GetRazorCSharpFilePath(razorFilePath));
            var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(csharpSourceText, csharpDocumentUri, new ServerCapabilities(), razorSpanMappingService: null).ConfigureAwait(false);
            await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString()).ConfigureAwait(false);

            DocumentContextFactory = new TestDocumentContextFactory(razorFilePath, codeDocument, version: 1337);
            LanguageServerFeatureOptions = Mock.Of<LanguageServerFeatureOptions>(options =>
                options.SupportsFileManipulation == true &&
                options.SingleServerSupport == true &&
                options.CSharpVirtualDocumentSuffix == realLanguageServerFeatureOptions.CSharpVirtualDocumentSuffix &&
                options.HtmlVirtualDocumentSuffix == realLanguageServerFeatureOptions.HtmlVirtualDocumentSuffix
                , MockBehavior.Strict);
            LanguageServer = new TestLanguageServer(csharpServer, csharpDocumentUri);
            DocumentMappingService = new DefaultRazorDocumentMappingService(LanguageServerFeatureOptions, DocumentContextFactory, LoggerFactory);
        }

        private class TestLanguageServer : ClientNotifierServiceBase
        {
            private readonly CSharpTestLspServer _csharpServer;
            private readonly Uri _csharpDocumentUri;

            public TestLanguageServer(CSharpTestLspServer csharpServer, Uri csharpDocumentUri)
            {
                _csharpServer = csharpServer;
                _csharpDocumentUri = csharpDocumentUri;
            }

            public override OmniSharp.Extensions.LanguageServer.Protocol.Models.InitializeParams ClientSettings { get; }

            public override Task OnStarted(ILanguageServer server, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public override Task<IResponseRouterReturns> SendRequestAsync(string method)
            {
                throw new NotImplementedException();
            }

            public async override Task<IResponseRouterReturns> SendRequestAsync<T>(string method, T @params)
            {
                return await (method switch
                {
                    RazorLanguageServerCustomMessageTargets.RazorDefinitionEndpointName => HandleDefinitionAsync(@params),
                    RazorLanguageServerCustomMessageTargets.RazorImplementationEndpointName => HandleImplementationAsync(@params),
                    RazorLanguageServerCustomMessageTargets.RazorSignatureHelpEndpointName => HandleSignatureHelpAsync(@params),
                    RazorLanguageServerCustomMessageTargets.RazorRenameEndpointName => HandleRenameAsync(@params),
                    _ => throw new NotImplementedException($"I don't know how to handle the '{method}' method.")
                });
            }

            private async Task<IResponseRouterReturns> HandleDefinitionAsync<T>(T @params)
            {
                var delegatedParams = Assert.IsType<DelegatedPositionParams>(@params);
                var delegatedRequest = new TextDocumentPositionParams()
                {
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = _csharpDocumentUri
                    },
                    Position = delegatedParams.ProjectedPosition
                };

                var result = await _csharpServer.ExecuteRequestAsync<TextDocumentPositionParams, DefinitionResult?>(Methods.TextDocumentDefinitionName, delegatedRequest, CancellationToken.None);

                return new TestResponseRouterReturn(result);
            }

            private async Task<IResponseRouterReturns> HandleImplementationAsync<T>(T @params)
            {
                var delegatedParams = Assert.IsType<DelegatedPositionParams>(@params);
                var delegatedRequest = new TextDocumentPositionParams()
                {
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = _csharpDocumentUri
                    },
                    Position = delegatedParams.ProjectedPosition
                };

                var result = await _csharpServer.ExecuteRequestAsync<TextDocumentPositionParams, ImplementationResult>(Methods.TextDocumentImplementationName, delegatedRequest, CancellationToken.None);

                return new TestResponseRouterReturn(result);
            }

            private async Task<IResponseRouterReturns> HandleSignatureHelpAsync<T>(T @params)
            {
                var delegatedParams = Assert.IsType<DelegatedPositionParams>(@params);
                var delegatedRequest = new SignatureHelpParams()
                {
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = _csharpDocumentUri
                    },
                    Position = delegatedParams.ProjectedPosition,
                };

                var result = await _csharpServer.ExecuteRequestAsync<SignatureHelpParams, VisualStudio.LanguageServer.Protocol.SignatureHelp>(Methods.TextDocumentSignatureHelpName, delegatedRequest, CancellationToken.None);

                return new TestResponseRouterReturn(result);
            }

            private async Task<IResponseRouterReturns> HandleRenameAsync<T>(T @params)
            {
                var delegatedParams = Assert.IsType<DelegatedRenameParams>(@params);
                var delegatedRequest = new RenameParams()
                {
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = _csharpDocumentUri
                    },
                    Position = delegatedParams.ProjectedPosition,
                    NewName = delegatedParams.NewName,
                };

                var result = await _csharpServer.ExecuteRequestAsync<RenameParams, WorkspaceEdit>(Methods.TextDocumentRenameName, delegatedRequest, CancellationToken.None);

                return new TestResponseRouterReturn(result);
            }
        }
    }
}
