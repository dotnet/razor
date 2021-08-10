// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#pragma warning disable CS0618 // Type or member is obsolete
#nullable enable

using System.Collections.Generic;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    /// <summary>
    /// </summary>
    internal class CSharpGeneratedSemanticTokensCache : SemanticTokensCacheBase<SemanticTokens>
    {
        public override SemanticTokens? GetMatchingTokenSet(List<SemanticTokens> tokenSets, string resultId)
            => tokenSets.FirstOrDefault(t => t.ResultId == resultId);
    }
}
