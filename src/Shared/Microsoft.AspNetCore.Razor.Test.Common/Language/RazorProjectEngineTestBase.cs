// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract class RazorProjectEngineTestBase
{
    protected abstract RazorLanguageVersion Version { get; }

    protected virtual void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
    }

    protected virtual void ConfigureProcessor(RazorCodeDocumentProcessor processor)
    {
    }

    protected RazorCodeDocumentProcessor InitializeDocument(RazorProjectEngine projectEngine, RazorCodeDocument codeDocument)
    {
        var processor = RazorCodeDocumentProcessor.From(projectEngine, codeDocument);
        ConfigureProcessor(processor);

        return processor;
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(string content, RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(content);
        return InitializeDocument(projectEngine, codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        string content,
        ImmutableArray<RazorSourceDocument> importSources,
        RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(content, importSources);
        return InitializeDocument(projectEngine, codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        string content,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor> tagHelpers,
        RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(content, importSources, tagHelpers);
        return InitializeDocument(projectEngine, codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        string content,
        IReadOnlyList<TagHelperDescriptor> tagHelpers,
        RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(content, tagHelpers);
        return InitializeDocument(projectEngine, codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(source);
        return InitializeDocument(projectEngine, codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        ImmutableArray<RazorSourceDocument> importSources,
        RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(source, importSources);
        return InitializeDocument(projectEngine, codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        ImmutableArray<RazorSourceDocument> importSources,
        IReadOnlyList<TagHelperDescriptor> tagHelpers,
        RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(source, importSources, tagHelpers);
        return InitializeDocument(projectEngine, codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeCodeDocument(
        RazorSourceDocument source,
        IReadOnlyList<TagHelperDescriptor> tagHelpers,
        RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(source, tagHelpers);
        return InitializeDocument(projectEngine, codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeDesignTimeCodeDocument(string content, RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateDesignTimeCodeDocument(content);
        return InitializeDocument(projectEngine, codeDocument);
    }

    protected RazorCodeDocumentProcessor CreateAndInitializeDesignTimeCodeDocument(
        string content,
        ImmutableArray<RazorSourceDocument> importSources,
        RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateDesignTimeCodeDocument(content, importSources);
        return InitializeDocument(projectEngine, codeDocument);
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
