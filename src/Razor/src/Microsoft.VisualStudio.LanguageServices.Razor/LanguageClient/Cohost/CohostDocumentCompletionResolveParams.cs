// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json;
using System;
using System.Text.Json.Serialization;
using Roslyn.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

// Data that's getting sent with each completion item so that we can provide document ID
// to Roslyn language server which will use the URI to figure out that language of the request
// and forward the request to us. It gets serialized as Data member of the completion item.
// Without it, Roslyn won't forward the completion resolve request to us.
internal sealed class CohostDocumentCompletionResolveParams
{
    // NOTE: Capital T here is required to match Roslyn's DocumentResolveData structure, so that the Roslyn
    //       language server can correctly route requests to us in cohosting. In future when we normalize
    //       on to Roslyn types, we should inherit from that class so we don't have to remember to do this.
    [JsonPropertyName("TextDocument")]
    public required VSTextDocumentIdentifier TextDocument { get; set; }

    public static CohostDocumentCompletionResolveParams Create(TextDocumentIdentifier textDocumentIdentifier)
    {
        var vsTextDocumentIdentifier = textDocumentIdentifier is VSTextDocumentIdentifier vsTextDocumentIdentifierValue
        ? vsTextDocumentIdentifierValue
            : new VSTextDocumentIdentifier() { Uri = textDocumentIdentifier.Uri };

        var resolutionParams = new CohostDocumentCompletionResolveParams()
        {
            TextDocument = vsTextDocumentIdentifier
        };

        return resolutionParams;
    }

    public static CohostDocumentCompletionResolveParams GetCohostDocumentCompletionResolveParams(VSInternalCompletionItem request)
    {
        if (request.Data is not JsonElement paramsObj)
        {
            throw new InvalidOperationException($"Invalid Completion Resolve Request Received");
        }

        var resolutionParams = paramsObj.Deserialize<CohostDocumentCompletionResolveParams>();
        if (resolutionParams is null)
        {
            throw new InvalidOperationException($"request.Data should be convertible to {nameof(CohostDocumentCompletionResolveParams)}");
        }

        return resolutionParams;
    }
}
