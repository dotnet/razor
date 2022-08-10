// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

// A file for delegated record types to be put. Each individually
// should be a plain record. If more logic is needed than record
// definition please put in a separate file. 
namespace Microsoft.AspNetCore.Razor.LanguageServer.Protocol;

internal record DelegatedHoverParams(
    VersionedTextDocumentIdentifier HostDocument,
    Position ProjectedPosition,
    RazorLanguageKind ProjectedKind);

internal record DelegatedCompletionParams(
        VersionedTextDocumentIdentifier HostDocument,
        Position ProjectedPosition,
        RazorLanguageKind ProjectedKind,
        VSInternalCompletionContext Context,
        TextEdit? ProvisionalTextEdit);

internal record DelegatedCompletionResolutionContext(
    DelegatedCompletionParams OriginalRequestParams,
    object? OriginalCompletionListData);

internal record DelegatedRenameParams(
    VersionedTextDocumentIdentifier HostDocument,
    Position ProjectedPosition,
    RazorLanguageKind ProjectedKind,
    string NewName);

internal record DelegatedCompletionItemResolveParams(
        TextDocumentIdentifier HostDocument,
        VSInternalCompletionItem CompletionItem,
        RazorLanguageKind OriginatingKind);
