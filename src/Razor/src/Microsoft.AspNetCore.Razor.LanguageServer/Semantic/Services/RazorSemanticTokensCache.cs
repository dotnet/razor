// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal sealed class RazorSemanticTokensCache : SemanticTokensCacheBase<VersionedSemanticTokens>
    {
        public override VersionedSemanticTokens GetMatchingTokenSet(List<VersionedSemanticTokens> tokenSets, string resultId)
            => tokenSets.FirstOrDefault(t => t.ResultId == resultId);
    }

    internal record VersionedSemanticTokens(VersionStamp SemanticVersion, string ResultId, IReadOnlyList<int> SemanticTokens);
}
