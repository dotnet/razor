// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract class RazorProjectEngineTestBase
{
    private RazorProjectEngine? _projectEngine;

    protected RazorProjectEngine ProjectEngine
        => _projectEngine ??= InterlockedOperations.Initialize(ref _projectEngine, CreateProjectEngine());

    protected abstract RazorLanguageVersion Version { get; }

    protected virtual void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
    }

    protected virtual void ConfigureProcessor(RazorCodeDocumentProcessor processor)
    {
    }

    protected RazorCodeDocumentProcessor InitializeDocument(RazorCodeDocument codeDocument)
    {
        var processor = RazorCodeDocumentProcessor.From(ProjectEngine, codeDocument);
        ConfigureProcessor(processor);

        return processor;
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(string content)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(content);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(string content, string fileKind)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(content, fileKind);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        string content,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(content, fileKind, importSources);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        string content,
        ImmutableArray<RazorSourceDocument> importSources)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(content, importSources);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        string content,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(content, fileKind, importSources, tagHelpers);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        string content,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(content, importSources, tagHelpers);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        string content,
        string fileKind,
        IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(content, fileKind, tagHelpers);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        string content,
        IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(content, tagHelpers);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        string fileKind)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(source, fileKind);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(RazorSourceDocument source)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(source);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(source, fileKind, importSources);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        ImmutableArray<RazorSourceDocument> importSources)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(source, importSources);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(source, fileKind, importSources, tagHelpers);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(source, importSources, tagHelpers);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        string fileKind,
        IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(source, fileKind, tagHelpers);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        var codeDocument = ProjectEngine.CreateCodeDocument(source, tagHelpers);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeDesignTimeCodeDocument(string content)
    {
        var codeDocument = ProjectEngine.CreateDesignTimeCodeDocument(content);
        return InitializeDocument(codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeDesignTimeCodeDocument(
        string content,
        ImmutableArray<RazorSourceDocument> importSources)
    {
        var codeDocument = ProjectEngine.CreateDesignTimeCodeDocument(content, importSources);
        return InitializeDocument(codeDocument);
    }

    protected RazorProjectEngine CreateProjectEngine()
    {
        var configuration = new RazorConfiguration(Version, "test", Extensions: []);
        return RazorProjectEngine.Create(configuration, RazorProjectFileSystem.Empty, ConfigureProjectEngine);
    }

    protected RazorProjectEngine CreateProjectEngine(Action<RazorProjectEngineBuilder> configure)
    {
        var configuration = new RazorConfiguration(Version, "test", Extensions: []);
        return RazorProjectEngine.Create(configuration, RazorProjectFileSystem.Empty, b =>
        {
            ConfigureProjectEngine(b);
            configure.Invoke(b);
        });
    }
}
