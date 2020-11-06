// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#pragma warning disable CS0618
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    public class ProvideSemanticTokensResponse
    {
        public SemanticTokens Result { get; private set; }

        public long? HostDocumentSyncVersion { get; private set; }

        public ProvideSemanticTokensResponse(SemanticTokens result, long? hostDocumentSyncVersion)
        {
            Result = result;
            HostDocumentSyncVersion = hostDocumentSyncVersion;
        }
    }
}
#pragma warning restore CS0618
