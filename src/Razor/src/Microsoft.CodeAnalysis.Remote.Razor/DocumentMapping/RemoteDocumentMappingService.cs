// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
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
    IProjectCollectionResolver projectCollectionResolver,
    ILoggerFactory loggerFactory)
    : AbstractDocumentMappingService(filePathService, loggerFactory.GetOrCreateLogger<RemoteDocumentMappingService>())
{
    private readonly IProjectCollectionResolver _projectCollectionResolver = projectCollectionResolver;

    public async Task<(Uri MappedDocumentUri, LinePositionSpan MappedRange)> MapToHostDocumentUriAndRangeAsync(
        RemoteDocumentSnapshot originSnapshot,
        Uri generatedDocumentUri,
        LinePositionSpan generatedDocumentRange,
        CancellationToken cancellationToken)
    {
        var razorDocumentUri = FilePathService.GetRazorDocumentUri(generatedDocumentUri);

        // For Html we just map the Uri, the range will be the same
        if (FilePathService.IsVirtualHtmlFile(generatedDocumentUri))
        {
            return (razorDocumentUri, generatedDocumentRange);
        }

        // We only map from C# files
        if (!FilePathService.IsVirtualCSharpFile(generatedDocumentUri))
        {
            return (generatedDocumentUri, generatedDocumentRange);
        }

        var razorFilePath = razorDocumentUri.GetDocumentFilePath();
        IDocumentSnapshot? razorDocumentSnapshot = null;

        foreach (var project in _projectCollectionResolver.EnumerateProjects(originSnapshot))
        {
            if (project.TryGetDocument(razorFilePath, out var snapshot))
            {
                razorDocumentSnapshot = snapshot;
                break;
            }
        }

        if (razorDocumentSnapshot is not RemoteDocumentSnapshot targetDocumentSnapshot)
        {
            return (generatedDocumentUri, generatedDocumentRange);
        }

        var codeDocument = await targetDocumentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (codeDocument is null)
        {
            return (generatedDocumentUri, generatedDocumentRange);
        }

        if (!codeDocument.TryGetGeneratedDocument(generatedDocumentUri, FilePathService, out var generatedDocument))
        {
            return Assumed.Unreachable<(Uri, LinePositionSpan)>();
        }

        if (TryMapToHostDocumentRange(generatedDocument, generatedDocumentRange, MappingBehavior.Strict, out var mappedRange))
        {
            return (razorDocumentUri, mappedRange);
        }

        return (generatedDocumentUri, generatedDocumentRange);
    }
}
