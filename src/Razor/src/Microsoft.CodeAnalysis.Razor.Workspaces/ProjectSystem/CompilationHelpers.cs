// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal static class CompilationHelpers
{
    internal static async Task<RazorCodeDocument> GenerateCodeDocumentAsync(DocumentSnapshot document, CancellationToken cancellationToken)
    {
        var project = document.Project;
        var projectEngine = project.ProjectEngine;
        var compilerOptions = project.CompilerOptions;

        var importSources = await GetImportSourcesAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);
        var tagHelpers = await document.Project.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
        var source = await GetSourceAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

        var generator = new CodeDocumentGenerator(projectEngine, compilerOptions);
        return generator.Generate(source, document.FileKind, importSources, tagHelpers, cancellationToken);
    }

    internal static async Task<RazorCodeDocument> GenerateDesignTimeCodeDocumentAsync(DocumentSnapshot document, CancellationToken cancellationToken)
    {
        var projectEngine = document.Project.ProjectEngine;

        var importSources = await GetImportSourcesAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);
        var tagHelpers = await document.Project.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
        var source = await GetSourceAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

        var generator = new CodeDocumentGenerator(projectEngine, RazorCompilerOptions.None);
        return generator.GenerateDesignTime(source, document.FileKind, importSources, tagHelpers, cancellationToken);
    }

    private static async Task<RazorSourceDocument> GetSourceAsync(
        DocumentSnapshot document,
        RazorProjectEngine projectEngine,
        CancellationToken cancellationToken)
    {
        var projectItem = document is { FilePath: string filePath, FileKind: var fileKind }
            ? projectEngine.FileSystem.GetItem(filePath, fileKind)
            : null;

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var properties = RazorSourceDocumentProperties.Create(document.FilePath, projectItem?.RelativePhysicalPath);
        return RazorSourceDocument.Create(text, properties);
    }

    private static async Task<ImmutableArray<RazorSourceDocument>> GetImportSourcesAsync(
        DocumentSnapshot document, RazorProjectEngine projectEngine, CancellationToken cancellationToken)
    {
        var projectItem = projectEngine.FileSystem.GetItem(document.FilePath, document.FileKind);

        using var importProjectItems = new PooledArrayBuilder<RazorProjectItem>();
        projectEngine.CollectImports(projectItem, ref importProjectItems.AsRef());

        if (importProjectItems.Count == 0)
        {
            return [];
        }

        var project = document.Project;

        using var importSources = new PooledArrayBuilder<RazorSourceDocument>(capacity: importProjectItems.Count);

        foreach (var importProjectItem in importProjectItems)
        {
            if (importProjectItem is NotFoundProjectItem)
            {
                continue;
            }

            if (importProjectItem is DefaultImportProjectItem)
            {
                var importSource = importProjectItem.GetSource()
                    .AssumeNotNull($"Encountered a default import with a missing {nameof(RazorSourceDocument)}: {importProjectItem.FilePath}.");

                importSources.Add(importSource);
            }
            else if (project.TryGetDocument(importProjectItem.PhysicalPath, out var importDocument))
            {
                var text = await importDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                var properties = RazorSourceDocumentProperties.Create(importProjectItem.FilePath, importProjectItem.RelativePhysicalPath);
                var importSource = RazorSourceDocument.Create(text, properties);

                importSources.Add(importSource);
            }
        }

        return importSources.DrainToImmutable();
    }
}
