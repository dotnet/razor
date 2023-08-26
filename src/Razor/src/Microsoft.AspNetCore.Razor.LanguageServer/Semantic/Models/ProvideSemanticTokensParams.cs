// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;

internal abstract class ProvideSemanticTokensParams : SemanticTokensParams
{
    [DataMember(Name = "requiredHostDocumentVersion", IsRequired = true)]
    public long RequiredHostDocumentVersion { get; }

    [DataMember(Name = "correlationId", IsRequired = true)]
    public Guid CorrelationId { get; }

    public ProvideSemanticTokensParams(TextDocumentIdentifier textDocument, long requiredHostDocumentVersion, Guid correlationId)
    {
        TextDocument = textDocument;
        RequiredHostDocumentVersion = requiredHostDocumentVersion;
        CorrelationId = correlationId;
    }
}
