// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class ProvideSemanticTokensResponse
    {
        public ProvideSemanticTokensResponse(SemanticTokensResponse result, long? hostDocumentSyncVersion)
        {
            Result = result;
            HostDocumentSyncVersion = hostDocumentSyncVersion;
        }

        public SemanticTokensResponse Result { get; }

        public long? HostDocumentSyncVersion { get; }

        public override bool Equals(object obj) =>
            obj is ProvideSemanticTokensResponse other &&
            other.HostDocumentSyncVersion.Equals(HostDocumentSyncVersion) && other.Result.Equals(Result);

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
