// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Services
{
    internal abstract class SemanticTokensCacheService
    {
        public abstract void CacheTokens(
            DocumentUri uri,
            VersionStamp semanticVersion,
            Range range,
            int[] tokens);

        public abstract void ClearCache();

        public abstract bool TryGetCachedTokens(
            DocumentUri uri,
            VersionStamp semanticVersion,
            Range requestedRange,
            ILogger? logger,
            [NotNullWhen(true)] out (Range Range, ImmutableArray<int> Tokens)? cachedTokens);
    }
}
