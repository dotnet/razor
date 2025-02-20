// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract class RazorProjectEngineTestBase
{
    private RazorProjectEngine? _projectEngine;

    protected RazorProjectEngine ProjectEngine
        => _projectEngine ??= InterlockedOperations.Initialize(ref _projectEngine, CreateProjectEngine());

    protected RazorConfiguration Configuration { get; }

    protected abstract RazorLanguageVersion Version { get; }

    protected RazorProjectEngineTestBase()
    {
        Configuration = new RazorConfiguration(Version, "test", Extensions: []);
    }

    protected virtual void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
    }

    protected virtual void ConfigureCodeDocumentProcessor(RazorCodeDocumentProcessor processor)
    {
    }

    protected RazorCodeDocumentProcessor CreateCodeDocumentProcessor(RazorCodeDocument codeDocument)
        => CreateCodeDocumentProcessor(ProjectEngine, codeDocument);

    protected RazorCodeDocumentProcessor CreateCodeDocumentProcessor(RazorProjectEngine projectEngine, RazorCodeDocument codeDocument)
    {
        var processor = RazorCodeDocumentProcessor.From(projectEngine, codeDocument);
        ConfigureCodeDocumentProcessor(processor);

        return processor;
    }

    protected RazorProjectEngine CreateProjectEngine()
        => RazorProjectEngine.Create(Configuration, RazorProjectFileSystem.Empty, ConfigureProjectEngine);

    protected RazorProjectEngine CreateProjectEngine(Action<RazorProjectEngineBuilder> configure)
    {
        return RazorProjectEngine.Create(Configuration, RazorProjectFileSystem.Empty, b =>
        {
            ConfigureProjectEngine(b);
            configure.Invoke(b);
        });
    }
}
