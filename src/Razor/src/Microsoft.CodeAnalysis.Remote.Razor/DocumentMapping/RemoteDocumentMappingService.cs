// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor.DocumentMapping;

[Export(typeof(IDocumentMappingService)), Shared]
[method: ImportingConstructor]
internal sealed class RemoteDocumentMappingService(
    IFilePathService filePathService,
    DocumentSnapshotFactory documentSnapshotFactory,
    ILoggerFactory loggerFactory)
    : AbstractDocumentMappingService(filePathService, loggerFactory.GetOrCreateLogger<RemoteDocumentMappingService>())
{
    private readonly DocumentSnapshotFactory _documentSnapshotFactory = documentSnapshotFactory;

    public async Task<(Uri MappedDocumentUri, LinePositionSpan MappedRange)> MapToHostDocumentUriAndRangeAsync(
        RemoteDocumentSnapshot originSnapshot,
        Uri generatedDocumentUri,
        LinePositionSpan generatedDocumentRange,
        CancellationToken cancellationToken)
    {
        // For Html we can just map the Uri, the range will be the same
        if (FilePathService.IsVirtualHtmlFile(generatedDocumentUri))
        {
            var razorDocumentUri = FilePathService.GetRazorDocumentUri(generatedDocumentUri);
            return (razorDocumentUri, generatedDocumentRange);
        }

        // We only map from C# files
        if (!FilePathService.IsVirtualCSharpFile(generatedDocumentUri))
        {
            return (generatedDocumentUri, generatedDocumentRange);
        }

        var solution = originSnapshot.TextDocument.Project.Solution;
        if (await solution.TryGetGeneratedRazorCodeDocumentAsync(generatedDocumentUri, cancellationToken).ConfigureAwait(false) is not { } razorCodeDocument)
        {
            return (generatedDocumentUri, generatedDocumentRange);
        }

        if (razorCodeDocument is null)
        {
            return (generatedDocumentUri, generatedDocumentRange);
        }

        var generatedDocument = razorCodeDocument.GetCSharpDocument();

        if (TryMapToHostDocumentRange(generatedDocument, generatedDocumentRange, MappingBehavior.Strict, out var mappedRange))
        {
            // TODO: Should we have a better way to do this? Store Uri in RazorCodeDocument?
            var filePath = razorCodeDocument.Source.FilePath;
            var documentId = solution.GetDocumentIdsWithFilePath(filePath).First();
            var document = solution.GetAdditionalDocument(documentId).AssumeNotNull();
            return (document.CreateUri(), mappedRange);
        }

        return (generatedDocumentUri, generatedDocumentRange);
    }
}
