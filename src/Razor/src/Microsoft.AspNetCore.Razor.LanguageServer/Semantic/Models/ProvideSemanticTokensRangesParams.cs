// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;

internal class ProvideSemanticTokensRangesParams : SemanticTokensParams
{
    [DataMember(Name = "requiredHostDocumentVersion", IsRequired = true)]
    public long RequiredHostDocumentVersion { get; }

    [DataMember(Name = "minimalRange", IsRequired = true)]
    public Range MinimalRange { get; }

    [DataMember(Name = "ranges", IsRequired = true)]
    public Range[] Ranges { get; }

    [DataMember(Name = "correlationId", IsRequired = true)]
    public Guid CorrelationId { get; }

    public ProvideSemanticTokensRangesParams(TextDocumentIdentifier textDocument, long requiredHostDocumentVersion, Range minimalRange, Range[] ranges, Guid correlationId)
    {
        TextDocument = textDocument;
        RequiredHostDocumentVersion = requiredHostDocumentVersion;
        MinimalRange = minimalRange;
        Ranges = ranges;
        CorrelationId = correlationId;
    }
}
