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
                    RazorLanguageServerCustomMessageTargets.RazorImplementationEndpointName => HandleImplementationAsync(@params),
                    _ => throw new NotImplementedException($"I don't know how to handle the '{method}' method.")
                });
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

                var result = await _csharpServer.ExecuteRequestAsync<TextDocumentPositionParams, SumType<Location[], VSInternalReferenceItem[]>>(Methods.TextDocumentImplementationName, delegatedRequest, CancellationToken.None);

                return new TestResponseRouterReturn(result);
            }
        }
    }
}
