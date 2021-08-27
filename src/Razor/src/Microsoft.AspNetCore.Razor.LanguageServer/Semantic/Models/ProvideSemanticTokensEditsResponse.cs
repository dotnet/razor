// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.
#nullable enable
using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class ProvideSemanticTokensEditsResponse
    {
        public ProvideSemanticTokensEditsResponse(
            int[]? tokens,
            RazorSemanticTokensEdit[]? edits,
            string? resultId,
            bool isPartial,
            long? hostDocumentSyncVersion)
        {
            Tokens = tokens;
            Edits = edits;
            ResultId = resultId;
            IsPartial = isPartial;
            HostDocumentSyncVersion = hostDocumentSyncVersion;
        }

        public int[]? Tokens { get; }

        public RazorSemanticTokensEdit[]? Edits { get; }

        public string? ResultId { get; }

        public bool IsPartial { get; }

        public long? HostDocumentSyncVersion { get; }

        public override bool Equals(object obj)
        {
            if (obj is not ProvideSemanticTokensEditsResponse other ||
                other.HostDocumentSyncVersion != HostDocumentSyncVersion ||
                other.ResultId != ResultId)
            {
                return false;
            }

            if (Tokens != null && other.Tokens != null && other.Tokens.SequenceEqual(Tokens))
            {
                return true;
            }

            if (Edits != null && other.Edits != null && other.Edits.SequenceEqual(Edits))
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
