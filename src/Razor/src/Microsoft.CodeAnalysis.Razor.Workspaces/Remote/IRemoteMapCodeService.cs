// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.MapCode;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteMapCodeService : IRemoteJsonService
{
    ValueTask<CSharpFocusLocationsAndNodes?> GetCSharpFocusLocationsAndNodesAsync(JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, string content, Guid correlationId, CancellationToken cancellationToken);
    ValueTask<ImmutableArray<TextDocumentEdit>> MapCSharpEditsAndRazorCodeAsync(JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo, string content, ImmutableArray<WorkspaceEdit> csharpEdits, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, CancellationToken cancellationToken);
}
