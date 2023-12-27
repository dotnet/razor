// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Cohost;

[Export(typeof(IRazorCohostDidOpenHandler)), Shared]
[method: ImportingConstructor]
internal class DidOpenHandler(
    ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
    RazorProjectService razorProjectService,
    ISnapshotResolver snapshotResolver,
    OpenDocumentGenerator openDocumentGenerator) : IRazorCohostDidOpenHandler
{
    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
    private readonly RazorProjectService _razorProjectService = razorProjectService;
    private readonly ISnapshotResolver _snapshotResolver = snapshotResolver;
    private readonly OpenDocumentGenerator _openDocumentGenerator = openDocumentGenerator;

    public async Task HandleAsync(Uri uri, int version, SourceText sourceText, CancellationToken cancellationToken)
    {
        await await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(async () =>
        {
            var textDocumentPath = FilePathNormalizer.Normalize(uri.GetAbsoluteOrUNCPath());
            _razorProjectService.OpenDocument(textDocumentPath, sourceText, version);

            if (!_snapshotResolver.TryResolveAllProjects(textDocumentPath, out var projectSnapshots))
            {
                projectSnapshots = [_snapshotResolver.GetMiscellaneousProject()];
            }

            foreach (var project in projectSnapshots)
            {
                var document = project.GetDocument(textDocumentPath);
                if (document is not null)
                {
                    await _openDocumentGenerator.DocumentOpenedOrChangedAsync(document, version, cancellationToken);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}
