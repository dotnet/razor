// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Protocol
{
    internal record DelegatedCompletionItemResolveParams(
        TextDocumentIdentifier HostDocument,
        VSInternalCompletionItem CompletionItem,
        RazorLanguageKind OriginatingKind);
}
