﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

// A file for delegated record types to be put. Each individually
// should be a plain record. If more logic is needed than record
// definition please put in a separate file.

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Protocol;

using Microsoft.VisualStudio.LanguageServer.Protocol;

internal record DelegatedSpellCheckParams(
    TextDocumentIdentifierAndVersion Identifier);

internal record DelegatedDiagnosticParams(
    TextDocumentIdentifierAndVersion Identifier,
    Guid CorrelationId);

internal record DelegatedPositionParams(
    TextDocumentIdentifierAndVersion Identifier,
    Position ProjectedPosition,
    RazorLanguageKind ProjectedKind) : IDelegatedParams;

internal record DelegatedInlayHintParams(
    TextDocumentIdentifierAndVersion Identifier,
    Range ProjectedRange,
    RazorLanguageKind ProjectedKind) : IDelegatedParams;

internal record DelegatedInlayHintResolveParams(
    TextDocumentIdentifierAndVersion Identifier,
    InlayHint InlayHint,
    RazorLanguageKind ProjectedKind) : IDelegatedParams;

internal record DelegatedValidateBreakpointRangeParams(
    TextDocumentIdentifierAndVersion Identifier,
    Range ProjectedRange,
    RazorLanguageKind ProjectedKind) : IDelegatedParams;

internal record DelegatedOnAutoInsertParams(
    TextDocumentIdentifierAndVersion Identifier,
    Position ProjectedPosition,
    RazorLanguageKind ProjectedKind,
    string Character,
    FormattingOptions Options) : IDelegatedParams;

internal record DelegatedRenameParams(
    TextDocumentIdentifierAndVersion Identifier,
    Position ProjectedPosition,
    RazorLanguageKind ProjectedKind,
    string NewName) : IDelegatedParams;

internal record DelegatedCompletionParams(
    TextDocumentIdentifierAndVersion Identifier,
    Position ProjectedPosition,
    RazorLanguageKind ProjectedKind,
    VSInternalCompletionContext Context,
    TextEdit? ProvisionalTextEdit,
    bool ShouldIncludeSnippets,
    Guid CorrelationId) : IDelegatedParams;

internal record DelegatedMapCodeParams(
    TextDocumentIdentifierAndVersion Identifier,
    RazorLanguageKind ProjectedKind,
    Guid MapCodeCorrelationId,
    string[] Contents,
    Location[][] FocusLocations) : IDelegatedParams;

internal record DelegatedCompletionResolutionContext(
    DelegatedCompletionParams OriginalRequestParams,
    object? OriginalCompletionListData);

internal record DelegatedCompletionItemResolveParams(
    TextDocumentIdentifierAndVersion Identifier,
    VSInternalCompletionItem CompletionItem,
    RazorLanguageKind OriginatingKind);

internal record DelegatedProjectContextsParams(
    Uri Uri);

internal record DelegatedDocumentSymbolParams(
    TextDocumentIdentifierAndVersion Identifier);

internal record DelegatedSimplifyMethodParams(
    TextDocumentIdentifierAndVersion Identifier,
    bool RequiresVirtualDocument,
    TextEdit TextEdit);
