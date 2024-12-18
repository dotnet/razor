// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal static class CompilationHelpers
{
    internal static RazorCodeDocument GenerateCodeDocument(
        RazorProjectEngine projectEngine,
        RazorCompilerOptions compilerOptions,
        RazorSourceDocument source,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        ImmutableArray<TagHelperDescriptor> tagHelpers)
    {
        var forceRuntimeCodeGeneration = compilerOptions.IsFlagSet(RazorCompilerOptions.ForceRuntimeCodeGeneration);

        return forceRuntimeCodeGeneration
            ? projectEngine.Process(source, fileKind, importSources, tagHelpers)
            : projectEngine.ProcessDesignTime(source, fileKind, importSources, tagHelpers);
    }

    internal static async Task<RazorCodeDocument> GenerateCodeDocumentAsync(
        IDocumentSnapshot document,
        RazorProjectEngine projectEngine,
        RazorCompilerOptions compilerOptions,
        CancellationToken cancellationToken)
    {
        var importItems = await ImportHelpers.GetImportItemsAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

        return await GenerateCodeDocumentAsync(
            document, projectEngine, importItems, compilerOptions, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<RazorCodeDocument> GenerateCodeDocumentAsync(
        IDocumentSnapshot document,
        RazorProjectEngine projectEngine,
        ImmutableArray<ImportItem> imports,
        RazorCompilerOptions compilerOptions,
        CancellationToken cancellationToken)
    {
        var importSources = ImportHelpers.GetImportSources(imports, projectEngine);
        var tagHelpers = await document.Project.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
        var source = await ImportHelpers.GetSourceAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

        var generator = new CodeDocumentGenerator(projectEngine, compilerOptions);
        return generator.Generate(source, document.FileKind, importSources, tagHelpers);
    }

    internal static async Task<RazorCodeDocument> GenerateDesignTimeCodeDocumentAsync(
        IDocumentSnapshot document,
        RazorProjectEngine projectEngine,
        ImmutableArray<ImportItem> imports,
        CancellationToken cancellationToken)
    {
        var importSources = ImportHelpers.GetImportSources(imports, projectEngine);
        var tagHelpers = await document.Project.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
        var source = await ImportHelpers.GetSourceAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

        var generator = new CodeDocumentGenerator(projectEngine, RazorCompilerOptions.None);
        return generator.GenerateDesignTime(source, document.FileKind, importSources, tagHelpers);
    }

    internal static async Task<RazorCodeDocument> GenerateDesignTimeCodeDocumentAsync(
        IDocumentSnapshot document,
        RazorProjectEngine projectEngine,
        CancellationToken cancellationToken)
    {
        var importItems = await ImportHelpers.GetImportItemsAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);
        var importSources = ImportHelpers.GetImportSources(importItems, projectEngine);
        var tagHelpers = await document.Project.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
        var source = await ImportHelpers.GetSourceAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

        var generator = new CodeDocumentGenerator(projectEngine, RazorCompilerOptions.None);
        return generator.GenerateDesignTime(source, document.FileKind, importSources, tagHelpers);
    }
}
