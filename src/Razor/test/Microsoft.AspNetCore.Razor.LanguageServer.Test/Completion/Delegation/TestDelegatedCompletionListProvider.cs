// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation
{
    internal class TestDelegatedCompletionListProvider : DelegatedCompletionListProvider
    {
        private readonly CompletionRequestResponseFactory _completionFactory;

        private TestDelegatedCompletionListProvider(DelegatedCompletionResponseRewriter[] responseRewriters, CompletionRequestResponseFactory completionFactory)
            : base(
                responseRewriters,
                new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), TestLoggerFactory.Instance),
                new TestLanguageServer(new Dictionary<string, Func<object, Task<object>>>()
                {
                    [LanguageServerConstants.RazorCompletionEndpointName] = completionFactory.OnDelegationAsync,
                }),
                new CompletionListCache())
        {
            _completionFactory = completionFactory;
        }

        public static TestDelegatedCompletionListProvider Create(params DelegatedCompletionResponseRewriter[] responseRewriters) =>
            Create(delegatedCompletionList: null, responseRewriters: responseRewriters);

        public static TestDelegatedCompletionListProvider Create(CSharpTestLspServer csharpServer, params DelegatedCompletionResponseRewriter[] responseRewriters)
        {
            var requestResponseFactory = new DelegatedCSharpCompletionRequestResponseFactory(csharpServer);
            var provider = new TestDelegatedCompletionListProvider(responseRewriters, requestResponseFactory);
            return provider;
        }

        public static TestDelegatedCompletionListProvider Create(VSInternalCompletionList delegatedCompletionList, params DelegatedCompletionResponseRewriter[] responseRewriters)
        {
            delegatedCompletionList ??= new VSInternalCompletionList()
            {
                Items = Array.Empty<CompletionItem>(),
            };
            var requestResponseFactory = new StaticCompletionRequestResponseFactory(delegatedCompletionList);
            var provider = new TestDelegatedCompletionListProvider(responseRewriters, requestResponseFactory);
            return provider;
        }

        public DelegatedCompletionParams DelegatedParams => _completionFactory.DelegatedParams;

        private class StaticCompletionRequestResponseFactory : CompletionRequestResponseFactory
        {
            private readonly VSInternalCompletionList _completionResponse;
            private DelegatedCompletionParams _delegatedParams;

            public StaticCompletionRequestResponseFactory(VSInternalCompletionList completionResponse)
            {
                _completionResponse = completionResponse;
            }

            public override DelegatedCompletionParams DelegatedParams => _delegatedParams;

            public override Task<object> OnDelegationAsync(object parameters)
            {
                _delegatedParams = (DelegatedCompletionParams)parameters;

                return Task.FromResult<object>(_completionResponse);
            }
        }

        private class DelegatedCSharpCompletionRequestResponseFactory : CompletionRequestResponseFactory
        {
            private readonly CSharpTestLspServer _csharpServer;
            private DelegatedCompletionParams _delegatedParams;

            public DelegatedCSharpCompletionRequestResponseFactory(CSharpTestLspServer csharpServer)
            {
                _csharpServer = csharpServer;
            }

            public override DelegatedCompletionParams DelegatedParams => _delegatedParams;

            public override async Task<object> OnDelegationAsync(object parameters)
            {
                var completionParams = (DelegatedCompletionParams)parameters;
                _delegatedParams = completionParams;

                var csharpDocumentPath = completionParams.HostDocument.Uri.OriginalString + "__virtual.g.cs";
                var csharpDocumentUri = new Uri(csharpDocumentPath);
                var csharpCompletionParams = new CompletionParams()
                {
                    Context = completionParams.Context,
                    Position = completionParams.ProjectedPosition,
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = csharpDocumentUri,
                    }
                };

                var delegatedCompletionList = await _csharpServer.ExecuteRequestAsync<CompletionParams, VSInternalCompletionList>(
                    Methods.TextDocumentCompletionName,
                    csharpCompletionParams,
                    CancellationToken.None).ConfigureAwait(false);

                return delegatedCompletionList;
            }
        }

        private abstract class CompletionRequestResponseFactory
        {
            public abstract DelegatedCompletionParams DelegatedParams { get; }

            public abstract Task<object> OnDelegationAsync(object parameters);
        }
    }
}
