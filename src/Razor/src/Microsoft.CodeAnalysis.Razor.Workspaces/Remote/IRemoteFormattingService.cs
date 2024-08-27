// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteFormattingService
{
    ValueTask<ImmutableArray<TextChange>> GetDocumentFormattingEditsAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId documentId, ImmutableArray<TextChange> htmlChanges, CancellationToken cancellationToken);
}
