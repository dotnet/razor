// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;

internal class ProvideSemanticTokensRangesParams : ProvideSemanticTokensParams
{
    [DataMember(Name = "ranges", IsRequired = true)]
    public Range[] Ranges { get; }

    public ProvideSemanticTokensRangesParams(TextDocumentIdentifier textDocument, long requiredHostDocumentVersion, Range[] ranges, Guid correlationId)
        : base(textDocument, requiredHostDocumentVersion, correlationId)
    {
        Ranges = ranges;
    }
}
