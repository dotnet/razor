// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

// A file for delegated record types to be put. Each individually
// should be a plain record. If more logic is needed than record
// definition please put in a separate file.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Protocol;

/// <summary>
/// Interface for delegated params that enables sharing of code in DefaultRazorLanguageServerCustomMessageTarget
/// </summary>
internal interface IDelegatedParams
{
    public VersionedTextDocumentIdentifier HostDocument { get; }
    public RazorLanguageKind ProjectedKind { get; }
}

internal record DelegatedPositionParams(
    VersionedTextDocumentIdentifier HostDocument,
    Position ProjectedPosition,
    RazorLanguageKind ProjectedKind) : IDelegatedParams;

internal record DelegatedRenameParams(
    VersionedTextDocumentIdentifier HostDocument,
    Position ProjectedPosition,
    RazorLanguageKind ProjectedKind,
    string NewName) : IDelegatedParams;

internal record DelegatedCompletionParams(
    VersionedTextDocumentIdentifier HostDocument,
    Position ProjectedPosition,
    RazorLanguageKind ProjectedKind,
    VSInternalCompletionContext Context,
    TextEdit? ProvisionalTextEdit) : IDelegatedParams;

internal record DelegatedCompletionResolutionContext(
    DelegatedCompletionParams OriginalRequestParams,
    object? OriginalCompletionListData);

internal record DelegatedCompletionItemResolveParams(
    TextDocumentIdentifier HostDocument,
    VSInternalCompletionItem CompletionItem,
    RazorLanguageKind OriginatingKind);
