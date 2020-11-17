// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#pragma warning disable CS0618
using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    public class ProvideSemanticTokensResponse
    {
        public SemanticTokens Result { get; }

        public long? HostDocumentSyncVersion { get; }

        public ProvideSemanticTokensResponse(SemanticTokens result, long? hostDocumentSyncVersion)
        {
            Result = result;
            HostDocumentSyncVersion = hostDocumentSyncVersion;
        }

        public override bool Equals(object obj)
        {
            if(obj is ProvideSemanticTokensResponse)
            {
                var other = obj as ProvideSemanticTokensResponse;

                return other.HostDocumentSyncVersion.Equals(HostDocumentSyncVersion) && other.Result.Equals(Result);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
#pragma warning restore CS0618
