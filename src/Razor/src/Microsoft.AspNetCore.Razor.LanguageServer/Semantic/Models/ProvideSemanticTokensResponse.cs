// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#pragma warning disable CS0618
#nullable enable
using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class ProvideSemanticTokensResponse
    {
        public ProvideSemanticTokensResponse(SemanticTokens? result, SemanticTokensDelta? editResult, long? hostDocumentSyncVersion)
        {
            Result = result;
            EditResult = editResult;
            HostDocumentSyncVersion = hostDocumentSyncVersion;
        }

        public SemanticTokens? Result { get; }

        public SemanticTokensDelta? EditResult { get; }

        public long? HostDocumentSyncVersion { get; }

        public override bool Equals(object obj)
        {
            if (!(obj is ProvideSemanticTokensResponse other) || !other.HostDocumentSyncVersion.Equals(HostDocumentSyncVersion))
            {
                return false;
            }

            if (Result != null && other.Result != null && other.Result.Equals(Result))
            {
                return true;
            }

            if (EditResult != null && other.EditResult != null && other.EditResult.Equals(EditResult))
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
#pragma warning restore CS0618
