// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class SemanticTokenLegendParams : IRequest<SemanticTokenLegend>, IBaseRequest
    {

    }

    public class SemanticTokenLegend
    {
        private static readonly string[] _tokenTypes = new string[] {
            "minimizedTagHelperDirectiveAttribute",
            "razorTagHelperElementStartTag",
            "razorTagHelperElementEndTag",
            "razorTagHelperAttribute",
            "tagHelperDirectiveAttribute"
        };

        private static readonly string[] _tokenModifiers = new string[] {
            "static", "async"
        };

        public IDictionary<string, int> TokenTypesLegend
        {
            get
            {
                return GetMap(_tokenTypes);
            }
        }

        public IDictionary<string, int> TokenModifiersLegend
        {
            get
            {
                return GetMap(_tokenModifiers);
            }
        }

        private static IDictionary<string, int> GetMap(IEnumerable<string> tokens)
        {
            var result = new Dictionary<string, int>();
            for (var i = 0; i < tokens.Count(); i++)
            {
                result.Add(tokens.ElementAt(i), i);
            }

            return result;

        }
    }

    public class SemanticTokenLegendCapability : DynamicCapability
    {

    }

    [Method("razor/semanticTokenLegend")]
    [Parallel]
    internal interface ISemanticTokenLegendHandler :
        IJsonRpcRequestHandler<SemanticTokenLegendParams, SemanticTokenLegend>,
        IRequestHandler<SemanticTokenLegendParams, SemanticTokenLegend>,
        IJsonRpcHandler,
        ICapability<SemanticTokenLegendCapability>
    {
    }

    public class RazorSemanticTokenLegendEndpoint : ISemanticTokenLegendHandler
    {
        public Task<SemanticTokenLegend> Handle(SemanticTokenLegendParams request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokenLegend());
        }

        public void SetCapability(SemanticTokenLegendCapability capability)
        {
            throw new NotImplementedException();
        }
    }
}
