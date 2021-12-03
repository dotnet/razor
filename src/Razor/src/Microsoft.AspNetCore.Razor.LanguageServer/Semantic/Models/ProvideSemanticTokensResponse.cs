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
        public ProvideSemanticTokensResponse(int[]? tokens, bool isFinalized, long? hostDocumentSyncVersion)
        {
            Tokens = tokens;
            IsFinalized = isFinalized;
            HostDocumentSyncVersion = hostDocumentSyncVersion;
        }

        public int[]? Tokens { get; }

        public bool IsFinalized { get; }

        public long? HostDocumentSyncVersion { get; }

        public override bool Equals(object obj)
        {
            if (obj is not ProvideSemanticTokensResponse other ||
                other.IsFinalized != IsFinalized ||
                other.HostDocumentSyncVersion != HostDocumentSyncVersion)
            {
                return false;
            }

            if (other.Tokens == Tokens || (other.Tokens is not null && Tokens is not null && other.Tokens.SequenceEqual(Tokens)))
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
