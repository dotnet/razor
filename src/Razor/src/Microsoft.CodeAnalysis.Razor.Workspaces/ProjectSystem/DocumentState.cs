// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal partial class DocumentState
{
    private static readonly LoadTextOptions s_loadTextOptions = new(SourceHashAlgorithm.Sha256);

    private static readonly TextAndVersion s_emptyTextAndVersion = TextAndVersion.Create(
        SourceText.From(string.Empty),
        VersionStamp.Default);

    public static readonly TextLoader EmptyLoader = TextLoader.From(s_emptyTextAndVersion);

    public HostDocument HostDocument { get; }
    public int Version { get; }

    private readonly TextLoader _textLoader;

    private readonly SemaphoreSlim _textAndVersionLock = new(initialCount: 1);
    private Task<TextAndVersion>? _cachedTextAndVersion;

    private readonly SemaphoreSlim _generatedOutputAndVersionLock = new(initialCount: 1);
    private Task<GeneratedOutputAndVersion>? _cachedGeneratedOutputAndVersion;

    private DocumentState(
        HostDocument hostDocument,
        int version,
        Task<TextAndVersion>? textAndVersion,
        TextLoader? textLoader)
    {
        HostDocument = hostDocument;
        Version = version;
        _cachedTextAndVersion = textAndVersion;
        _textLoader = textLoader ?? EmptyLoader;
    }

    // Internal for testing
    internal DocumentState(HostDocument hostDocument, int version, SourceText text, VersionStamp textVersion)
        : this(hostDocument, version, Task.FromResult(TextAndVersion.Create(text, textVersion)), textLoader: null)
    {
    }

    // Internal for testing
    internal DocumentState(HostDocument hostDocument, int version, TextLoader loader)
        : this(hostDocument, version, textAndVersion: null, loader)
    {
    }

    public static DocumentState Create(HostDocument hostDocument, int version, SourceText text, VersionStamp textVersion)
    {
        return new DocumentState(hostDocument, version, text, textVersion);
    }

    public static DocumentState Create(HostDocument hostDocument, int version, TextLoader loader)
    {
        return new DocumentState(hostDocument, version, loader);
    }

    public static DocumentState Create(HostDocument hostDocument, TextLoader loader)
    {
        return new DocumentState(hostDocument, version: 1, loader);
    }

    public bool TryGetGeneratedOutputAndVersion([NotNullWhen(true)] out GeneratedOutputAndVersion? result)
    {
        if (_cachedGeneratedOutputAndVersion is not null)
        {
            result = _cachedGeneratedOutputAndVersion.VerifyCompleted();
            return true;
        }

        result = null;
        return false;
    }

    public async Task<GeneratedOutputAndVersion> GetGeneratedOutputAndVersionAsync(
        ProjectSnapshot project,
        DocumentSnapshot document,
        CancellationToken cancellationToken)
    {
        if (TryGetGeneratedOutputAndVersion(out var result))
        {
            return result;
        }

        using (await _generatedOutputAndVersionLock.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_cachedGeneratedOutputAndVersion is not null)
            {
                return _cachedGeneratedOutputAndVersion.VerifyCompleted();
            }

            // We only need to produce the generated code if any of our inputs is newer than the
            // previously cached output.
            //
            // First find the versions that are the inputs:
            // - The project + computed state
            // - The imports
            // - This document
            //
            // All of these things are cached, so no work is wasted if we do need to generate the code.
            var configurationVersion = project.ConfigurationVersion;
            var projectWorkspaceStateVersion = project.ProjectWorkspaceStateVersion;
            var documentCollectionVersion = project.DocumentCollectionVersion;
            var importItems = await GetImportItemsAsync(document, project.GetProjectEngine(), cancellationToken).ConfigureAwait(false);
            var documentVersion = await document.GetTextVersionAsync(cancellationToken).ConfigureAwait(false);

            // OK now that have the previous output and all of the versions, we can see if anything
            // has changed that would require regenerating the code.
            var inputVersion = documentVersion;
            if (inputVersion.GetNewerVersion(configurationVersion) == configurationVersion)
            {
                inputVersion = configurationVersion;
            }

            if (inputVersion.GetNewerVersion(projectWorkspaceStateVersion) == projectWorkspaceStateVersion)
            {
                inputVersion = projectWorkspaceStateVersion;
            }

            if (inputVersion.GetNewerVersion(documentCollectionVersion) == documentCollectionVersion)
            {
                inputVersion = documentCollectionVersion;
            }

            foreach (var import in importItems)
            {
                var importVersion = import.Version;
                if (inputVersion.GetNewerVersion(importVersion) == importVersion)
                {
                    inputVersion = importVersion;
                }
            }

            var forceRuntimeCodeGeneration = project.LanguageServerFeatureOptions.ForceRuntimeCodeGeneration;
            var codeDocument = await GenerateCodeDocumentAsync(document, project.GetProjectEngine(), importItems, forceRuntimeCodeGeneration, cancellationToken).ConfigureAwait(false);

            result = new GeneratedOutputAndVersion(codeDocument, inputVersion);
            _cachedGeneratedOutputAndVersion = Task.FromResult(result);

            return result;
        }
    }

    public ValueTask<TextAndVersion> GetTextAndVersionAsync(CancellationToken cancellationToken)
    {
        return TryGetTextAndVersion(out var result)
            ? new(result)
            : LoadTextAndVersionAsync(_textLoader, cancellationToken);

        async ValueTask<TextAndVersion> LoadTextAndVersionAsync(TextLoader loader, CancellationToken cancellationToken)
        {
            using (await _textAndVersionLock.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_cachedTextAndVersion is not null)
                {
                    return _cachedTextAndVersion.VerifyCompleted();
                }

                var task = loader.LoadTextAndVersionAsync(s_loadTextOptions, cancellationToken);
                var textAndVersion = await task.ConfigureAwait(false);

                _cachedTextAndVersion = task;

                return textAndVersion;
            }
        }
    }

    public ValueTask<SourceText> GetTextAsync(CancellationToken cancellationToken)
    {
        return TryGetText(out var text)
            ? new(text)
            : GetTextCoreAsync(cancellationToken);

        async ValueTask<SourceText> GetTextCoreAsync(CancellationToken cancellationToken)
        {
            var textAsVersion = await GetTextAndVersionAsync(cancellationToken).ConfigureAwait(false);

            return textAsVersion.Text;
        }
    }

    public ValueTask<VersionStamp> GetTextVersionAsync(CancellationToken cancellationToken)
    {
        return TryGetTextVersion(out var version)
            ? new(version)
            : GetTextVersionCoreAsync(cancellationToken);

        async ValueTask<VersionStamp> GetTextVersionCoreAsync(CancellationToken cancellationToken)
        {
            var textAsVersion = await GetTextAndVersionAsync(cancellationToken).ConfigureAwait(false);

            return textAsVersion.Version;
        }
    }

    public bool TryGetTextAndVersion([NotNullWhen(true)] out TextAndVersion? result)
    {
        if (_cachedTextAndVersion is not null)
        {
            result = _cachedTextAndVersion.VerifyCompleted();
            return true;
        }

        result = null;
        return false;
    }

    public bool TryGetText([NotNullWhen(true)] out SourceText? result)
    {
        if (TryGetTextAndVersion(out var textAndVersion))
        {
            result = textAndVersion.Text;
            return true;
        }

        result = null;
        return false;
    }

    public bool TryGetTextVersion(out VersionStamp result)
    {
        if (TryGetTextAndVersion(out var textAndVersion))
        {
            result = textAndVersion.Version;
            return true;
        }

        result = default;
        return false;
    }

    public virtual DocumentState WithConfigurationChange()
    {
        return new DocumentState(HostDocument, Version + 1, _cachedTextAndVersion, _textLoader);
    }

    public virtual DocumentState WithImportsChange()
    {
        return new DocumentState(HostDocument, Version + 1, _cachedTextAndVersion, _textLoader);
    }

    public virtual DocumentState WithProjectWorkspaceStateChange()
    {
        return new DocumentState(HostDocument, Version + 1, _cachedTextAndVersion, _textLoader);
    }

    public virtual DocumentState WithText(SourceText text, VersionStamp textVersion)
    {
        return new DocumentState(HostDocument, Version + 1, Task.FromResult(TextAndVersion.Create(text, textVersion)), textLoader: null);
    }

    public virtual DocumentState WithTextLoader(TextLoader textLoader)
    {
        return new DocumentState(HostDocument, Version + 1, textAndVersion: null, textLoader);
    }

    internal static async Task<RazorCodeDocument> GenerateCodeDocumentAsync(
        IDocumentSnapshot document,
        RazorProjectEngine projectEngine,
        bool forceRuntimeCodeGeneration,
        CancellationToken cancellationToken)
    {
        var importItems = await GetImportItemsAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

        return await GenerateCodeDocumentAsync(
            document, projectEngine, importItems, forceRuntimeCodeGeneration, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<RazorCodeDocument> GenerateCodeDocumentAsync(
        IDocumentSnapshot document,
        RazorProjectEngine projectEngine,
        ImmutableArray<ImportItem> imports,
        bool forceRuntimeCodeGeneration,
        CancellationToken cancellationToken)
    {
        var importSources = GetImportSources(imports, projectEngine);
        var tagHelpers = await document.Project.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
        var source = await GetSourceAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

        return forceRuntimeCodeGeneration
            ? projectEngine.Process(source, document.FileKind, importSources, tagHelpers)
            : projectEngine.ProcessDesignTime(source, document.FileKind, importSources, tagHelpers);
    }

    private static async Task<ImmutableArray<ImportItem>> GetImportItemsAsync(IDocumentSnapshot document, RazorProjectEngine projectEngine, CancellationToken cancellationToken)
    {
        var projectItem = projectEngine.FileSystem.GetItem(document.FilePath, document.FileKind);

        using var importProjectItems = new PooledArrayBuilder<RazorProjectItem>();

        foreach (var feature in projectEngine.ProjectFeatures.OfType<IImportProjectFeature>())
        {
            if (feature.GetImports(projectItem) is { } featureImports)
            {
                importProjectItems.AddRange(featureImports);
            }
        }

        if (importProjectItems.Count == 0)
        {
            return [];
        }

        var project = document.Project;

        using var importItems = new PooledArrayBuilder<ImportItem>(capacity: importProjectItems.Count);

        foreach (var importProjectItem in importProjectItems)
        {
            if (importProjectItem is NotFoundProjectItem)
            {
                continue;
            }

            if (importProjectItem.PhysicalPath is null)
            {
                // This is a default import.
                using var stream = importProjectItem.Read();
                var text = SourceText.From(stream);
                var defaultImport = ImportItem.CreateDefault(text);

                importItems.Add(defaultImport);
            }
            else if (project.TryGetDocument(importProjectItem.PhysicalPath, out var importDocument))
            {
                var text = await importDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                var versionStamp = await importDocument.GetTextVersionAsync(cancellationToken).ConfigureAwait(false);
                var importItem = new ImportItem(importDocument.FilePath, importDocument.FileKind, text, versionStamp);

                importItems.Add(importItem);
            }
        }

        return importItems.DrainToImmutable();
    }

    private static ImmutableArray<RazorSourceDocument> GetImportSources(ImmutableArray<ImportItem> importItems, RazorProjectEngine projectEngine)
    {
        using var importSources = new PooledArrayBuilder<RazorSourceDocument>(importItems.Length);

        foreach (var importItem in importItems)
        {
            var importProjectItem = importItem is { FilePath: string filePath, FileKind: var fileKind }
                ? projectEngine.FileSystem.GetItem(filePath, fileKind)
                : null;

            var properties = RazorSourceDocumentProperties.Create(importItem.FilePath, importProjectItem?.RelativePhysicalPath);
            var importSource = RazorSourceDocument.Create(importItem.Text, properties);

            importSources.Add(importSource);
        }

        return importSources.DrainToImmutable();
    }

    private static async Task<RazorSourceDocument> GetSourceAsync(IDocumentSnapshot document, RazorProjectEngine projectEngine, CancellationToken cancellationToken)
    {
        var projectItem = document is { FilePath: string filePath, FileKind: var fileKind }
            ? projectEngine.FileSystem.GetItem(filePath, fileKind)
            : null;

        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var properties = RazorSourceDocumentProperties.Create(document.FilePath, projectItem?.RelativePhysicalPath);
        return RazorSourceDocument.Create(text, properties);
    }
}
