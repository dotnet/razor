// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation
{
    internal class TestDelegatedCompletionListProvider : DelegatedCompletionListProvider
    {
        private readonly DelegatedCompletionRequestResponseFactory _completionFactory;

        private TestDelegatedCompletionListProvider(DelegatedCompletionResponseRewriter[] responseRewriters, ILoggerFactory loggerFactory, DelegatedCompletionRequestResponseFactory completionFactory) :
            base(
                responseRewriters,
                new DefaultRazorDocumentMappingService(loggerFactory),
                new TestOmnisharpLanguageServer(new Dictionary<string, Func<object, object>>()
                {
                    [LanguageServerConstants.RazorCompletionEndpointName] = completionFactory.OnDelegation,
                }))
        {
            _completionFactory = completionFactory;
        }

        public static TestDelegatedCompletionListProvider Create(ILoggerFactory loggerFactory, params DelegatedCompletionResponseRewriter[] responseRewriters) =>
            Create(loggerFactory, delegatedCompletionList: null, responseRewriters);

        public static TestDelegatedCompletionListProvider Create(ILoggerFactory loggerFactory, VSInternalCompletionList delegatedCompletionList, params DelegatedCompletionResponseRewriter[] responseRewriters)
        {
            delegatedCompletionList ??= new VSInternalCompletionList()
            {
                Items = Array.Empty<CompletionItem>(),
            };
            var requestResponseFactory = new DelegatedCompletionRequestResponseFactory(delegatedCompletionList);
            var provider = new TestDelegatedCompletionListProvider(responseRewriters, loggerFactory, requestResponseFactory);
            return provider;
        }

        public DelegatedCompletionParams DelegatedParams => _completionFactory.DelegatedParams;

        private class DelegatedCompletionRequestResponseFactory
        {
            private readonly VSInternalCompletionList _completionResponse;

            public DelegatedCompletionRequestResponseFactory(VSInternalCompletionList completionResponse)
            {
                _completionResponse = completionResponse;
            }

            public DelegatedCompletionParams DelegatedParams { get; private set; }

            public object OnDelegation(object parameters)
            {
                DelegatedParams = (DelegatedCompletionParams)parameters;

                return _completionResponse;
            }
        }
    }
}
