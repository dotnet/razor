// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    /// <summary>
    /// Transports C# semantic token responses from the Razor LS client to the Razor LS.
    /// </summary>
    internal class ProvideSemanticTokensResponse
    {
        public ProvideSemanticTokensResponse(string? resultId, int[]? tokens, bool isPartial, long? hostDocumentSyncVersion)
        {
            ResultId = resultId;
            Tokens = tokens;
            IsPartial = isPartial;
            HostDocumentSyncVersion = hostDocumentSyncVersion;
        }

        public string? ResultId { get; }

        public int[]? Tokens { get; }

        public bool IsPartial { get; }

        public long? HostDocumentSyncVersion { get; }

        public override bool Equals(object obj)
        {
            if (obj is not ProvideSemanticTokensResponse other ||
                other.ResultId != ResultId ||
                other.IsPartial != IsPartial ||
                other.HostDocumentSyncVersion != HostDocumentSyncVersion)
            {
                return false;
            }

            if (Tokens is not null && other.Tokens is not null && other.Tokens.SequenceEqual(Tokens))
            {
                return true;
            }

            return false;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
