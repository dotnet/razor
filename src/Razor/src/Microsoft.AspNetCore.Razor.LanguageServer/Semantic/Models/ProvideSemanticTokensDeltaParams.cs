// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
#pragma warning disable CS0618 // Type or member is obsolete
    internal class ProvideSemanticTokensDeltaParams : SemanticTokensDeltaParams
#pragma warning restore CS0618 // Type or member is obsolete
    {
        public long RequiredHostDocumentVersion { get; set; }
    }
}
