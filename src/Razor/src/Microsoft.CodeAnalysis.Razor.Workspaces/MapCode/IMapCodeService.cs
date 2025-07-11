// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.MapCode;

internal interface IMapCodeService
{
    Task<CSharpFocusLocationsAndNodes?> GetCSharpFocusLocationsAndNodesAsync(ISolutionQueryOperations queryOperations, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, string? content, CancellationToken cancellationToken);
    Task<WorkspaceEdit?> MapCodeAsync(ISolutionQueryOperations solutionQueryOperations, VSInternalMapCodeMapping[] mappings, Guid mapCodeCorrelationId, CancellationToken cancellationToken);
    Task MapCSharpEditsAndRazorCodeAsync(ISolutionQueryOperations queryOperations, string content, List<TextDocumentEdit> changes, ImmutableArray<WorkspaceEdit> csharpEdits, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, CancellationToken cancellationToken);
}
