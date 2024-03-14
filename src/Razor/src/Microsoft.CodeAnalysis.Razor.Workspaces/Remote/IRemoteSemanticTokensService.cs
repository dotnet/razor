// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal interface IRemoteSemanticTokensService
{
    ValueTask<int[]?> GetSemanticTokensDataAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        LinePositionSpan span,
        bool colorBackground,
        string[] tokenTypes,
        string[] tokenModifiers,
        CancellationToken cancellationToken);
}
