// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.MapCode;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Remote.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor.MapCode;

[Export(typeof(IMapCodeService)), Shared]
[method: ImportingConstructor]
internal class OOPMapCodeService(
    IDocumentMappingService documentMappingService)
    : AbstractMapCodeService(documentMappingService)
{
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;

    protected override bool TryCreateDocumentContext(ISolutionQueryOperations queryOperations, Uri uri, [NotNullWhen(true)] out DocumentContext? documentContext)
    {
        var filePath = RazorUri.GetDocumentFilePathFromUri(uri);
        if (queryOperations.GetProjectsContainingDocument(filePath) is [{ } project] &&
            project.GetDocument(filePath) is { } snapshot)
        {
            documentContext = new RemoteDocumentContext(uri, (RemoteDocumentSnapshot)snapshot);
            return true;
        }

        documentContext = null;
        return false;
    }

    protected async override Task<(Uri MappedDocumentUri, LinePositionSpan MappedRange)> MapToHostDocumentUriAndRangeAsync(DocumentContext documentContext, Uri generatedDocumentUri, LinePositionSpan generatedDocumentRange, CancellationToken cancellationToken)
    {
        Debug.Assert(documentContext is RemoteDocumentContext, "This method only works on document snapshots created in the OOP process");
        var snapshot = (RemoteDocumentSnapshot)documentContext.Snapshot;
        return await _documentMappingService.MapToHostDocumentUriAndRangeAsync(snapshot, generatedDocumentUri, generatedDocumentRange, cancellationToken).ConfigureAwait(false);
    }
}
