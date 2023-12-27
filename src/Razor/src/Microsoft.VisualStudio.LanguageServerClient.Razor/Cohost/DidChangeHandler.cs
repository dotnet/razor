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

[Export(typeof(IRazorCohostDidChangeHandler)), Shared]
[method: ImportingConstructor]
internal class DidChangeHandler(
    ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
    RazorProjectService razorProjectService,
    ISnapshotResolver snapshotResolver,
    OpenDocumentGenerator openDocumentGenerator) : IRazorCohostDidChangeHandler
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
            _razorProjectService.UpdateDocument(textDocumentPath, sourceText, version);

            // We are purposefully trigger code generation here directly, rather than using the project manager events that the above call
            // would have triggered, because Cohosting is intended to eventually remove the project manager and its events. We also want
            // to eventually remove this code too, and just rely on the source generator, but by keeping the concepts separate we are not
            // tying the code to any particular order of feature removal.
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
