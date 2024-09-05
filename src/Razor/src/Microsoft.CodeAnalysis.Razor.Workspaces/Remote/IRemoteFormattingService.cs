﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteFormattingService
{
    ValueTask<ImmutableArray<TextChange>> GetDocumentFormattingEditsAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId documentId,
        ImmutableArray<TextChange> htmlChanges,
        RazorFormattingOptions options,
        CancellationToken cancellationToken);

    ValueTask<ImmutableArray<TextChange>> GetRangeFormattingEditsAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId documentId,
        ImmutableArray<TextChange> htmlChanges,
        LinePositionSpan linePositionSpan,
        RazorFormattingOptions options,
        CancellationToken cancellationToken);

    ValueTask<ImmutableArray<TextChange>> GetOnTypeFormattingEditsAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId documentId,
        ImmutableArray<TextChange> htmlChanges,
        LinePosition linePosition,
        string triggerCharacter,
        RazorFormattingOptions options,
        CancellationToken cancellationToken);

    ValueTask<TriggerKind> GetOnTypeFormattingTriggerKindAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId documentId,
        LinePosition linePosition,
        string triggerCharacter,
        CancellationToken cancellationToken);

    internal enum TriggerKind
    {
        Invalid,
        ValidHtml,
        ValidCSharp,
    }
}
