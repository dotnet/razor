// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

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

    protected RazorCodeDocumentProcessor CreateAndInitializeDesignTimeCodeDocument(string content, RazorProjectEngine? projectEngine = null)
    {
        projectEngine ??= CreateProjectEngine();

        var codeDocument = projectEngine.CreateDesignTimeCodeDocument(content);
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
