// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    internal class ProvideSemanticTokensRangeParams : SemanticTokensParams
    {
        public long RequiredHostDocumentVersion { get; }

        public Range Range { get; }

        public ProvideSemanticTokensRangeParams(TextDocumentIdentifier textDocument, long requiredHostDocumentVersion, Range range)
        {
            TextDocument = textDocument;
            RequiredHostDocumentVersion = requiredHostDocumentVersion;
            Range = range;
        }
    }
}
