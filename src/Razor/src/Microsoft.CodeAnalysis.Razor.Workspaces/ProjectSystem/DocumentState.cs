// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal sealed partial class DocumentState
{
    private static readonly LoadTextOptions s_loadTextOptions = new(SourceHashAlgorithm.Sha256);

    public HostDocument HostDocument { get; }
    public int Version { get; }

    private TextAndVersion? _textAndVersion;
    private readonly TextLoader _textLoader;

    private ComputedStateTracker? _computedState;

    private DocumentState(
        HostDocument hostDocument,
        TextAndVersion? textAndVersion,
        TextLoader? textLoader)
    {
        HostDocument = hostDocument;
        Version = 1;

        _textAndVersion = textAndVersion;
        _textLoader = textLoader ?? EmptyTextLoader.Instance;
    }

    private DocumentState(
        DocumentState oldState,
        TextAndVersion? textAndVersion,
        TextLoader? textLoader,
        ComputedStateTracker? computedState = null)
    {
        HostDocument = oldState.HostDocument;
        Version = oldState.Version + 1;

        _textAndVersion = textAndVersion;
        _textLoader = textLoader ?? EmptyTextLoader.Instance;
        _computedState = computedState;
    }

    public static DocumentState Create(HostDocument hostDocument, SourceText text)
        => new(hostDocument, TextAndVersion.Create(text, VersionStamp.Create()), textLoader: null);

    public static DocumentState Create(HostDocument hostDocument, TextLoader loader)
        => new(hostDocument, textAndVersion: null, loader);

    private ComputedStateTracker ComputedState
        => _computedState ??= InterlockedOperations.Initialize(ref _computedState, new ComputedStateTracker());

    public bool TryGetGeneratedOutputAndVersion(out (RazorCodeDocument output, VersionStamp inputVersion) result)
    {
        return ComputedState.TryGetGeneratedOutputAndVersion(out result);
    }

    public Task<(RazorCodeDocument output, VersionStamp inputVersion)> GetGeneratedOutputAndVersionAsync(
        ProjectSnapshot project,
        DocumentSnapshot document,
        CancellationToken cancellationToken)
    {
        return ComputedState.GetGeneratedOutputAndVersionAsync(project, document, cancellationToken);
    }

    public ValueTask<TextAndVersion> GetTextAndVersionAsync(CancellationToken cancellationToken)
    {
        return TryGetTextAndVersion(out var result)
            ? new(result)
            : LoadTextAndVersionAsync(_textLoader, cancellationToken);

        async ValueTask<TextAndVersion> LoadTextAndVersionAsync(TextLoader loader, CancellationToken cancellationToken)
        {
            var textAndVersion = await loader
                .LoadTextAndVersionAsync(s_loadTextOptions, cancellationToken)
                .ConfigureAwait(false);

            return InterlockedOperations.Initialize(ref _textAndVersion, textAndVersion);
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
        result = _textAndVersion;
        return result is not null;
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

    public DocumentState WithConfigurationChange()
        => new(this, _textAndVersion, _textLoader, computedState: null);

    public DocumentState WithImportsChange()
        => new(this, _textAndVersion, _textLoader, new ComputedStateTracker(_computedState));

    public DocumentState WithProjectWorkspaceStateChange()
        => new(this, _textAndVersion, _textLoader, new ComputedStateTracker(_computedState));

    public DocumentState WithText(SourceText text, VersionStamp textVersion)
        => new(this, TextAndVersion.Create(text, textVersion), textLoader: null, computedState: null);

    public DocumentState WithTextLoader(TextLoader textLoader)
        => new(this, textAndVersion: null, textLoader, computedState: null);

    internal static async Task<RazorCodeDocument> GenerateCodeDocumentAsync(
        IDocumentSnapshot document,
        RazorProjectEngine projectEngine,
        bool forceRuntimeCodeGeneration,
        CancellationToken cancellationToken)
    {
        var importItems = await ImportHelpers.GetImportItemsAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

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
        var importSources = ImportHelpers.GetImportSources(imports, projectEngine);
        var tagHelpers = await document.Project.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
        var source = await ImportHelpers.GetSourceAsync(document, projectEngine, cancellationToken).ConfigureAwait(false);

        return forceRuntimeCodeGeneration
            ? projectEngine.Process(source, document.FileKind, importSources, tagHelpers)
            : projectEngine.ProcessDesignTime(source, document.FileKind, importSources, tagHelpers);
    }
}
