// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;

internal class ProvideSemanticTokensRangeParams : ProvideSemanticTokensParams
{
    [DataMember(Name = "range", IsRequired = true)]
    public Range Range { get; }

    public ProvideSemanticTokensRangeParams(TextDocumentIdentifier textDocument, long requiredHostDocumentVersion, Range range, Guid correlationId)
        : base(textDocument, requiredHostDocumentVersion, correlationId)
    {
        Range = range;
    }
}
