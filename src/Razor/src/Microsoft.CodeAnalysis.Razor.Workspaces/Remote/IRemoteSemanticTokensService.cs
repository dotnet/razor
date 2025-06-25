﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteSemanticTokensService
{
    ValueTask<int[]?> GetSemanticTokensDataAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        LinePositionSpan span,
        bool colorBackground,
        Guid correlationId,
        CancellationToken cancellationToken);
}
