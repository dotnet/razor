// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    /// <summary>
    /// Transports C# semantic token edits responses from the Razor LS client to the Razor LS.
    /// </summary>
    /// <remarks>
    /// Only one of Tokens or Edits should be populated except in error cases.
    /// </remarks>
    internal class ProvideSemanticTokensEditsResponse : ProvideSemanticTokensResponse
    {
        public ProvideSemanticTokensEditsResponse(
            string? resultId,
            int[]? tokens,
            RazorSemanticTokensEdit[]? edits,
            bool isPartial,
            long? hostDocumentSyncVersion)
            : base(resultId, tokens ?? Array.Empty<int>(), isPartial, hostDocumentSyncVersion)
        {
            Edits = edits;
        }

        public RazorSemanticTokensEdit[]? Edits { get; }

        public override bool Equals(object obj)
        {
            if (obj is not ProvideSemanticTokensEditsResponse other)
            {
                return false;
            }

            if (!base.Equals(obj))
            {
                return false;
            }

            if (other.Edits == Edits || (other.Edits is not null && Edits is not null && other.Edits.SequenceEqual(Edits)))
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
