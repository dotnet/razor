// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    [Obsolete("Proposed for the next version of the language server. May not work with all clients.  May be removed or changed in the future.")]
    public class ProvideSemanticTokensParams : SemanticTokensParams
    {
        public long RequiredHostDocumentVersion { get; set; }
    }
}
