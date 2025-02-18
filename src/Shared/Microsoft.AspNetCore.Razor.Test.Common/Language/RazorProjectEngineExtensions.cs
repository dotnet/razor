// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

public static class RazorProjectEngineExtensions
{
    private static string DefaultFileKind => FileKinds.Legacy;

    public static RazorCodeDocument CreateEmptyCodeDocument(this RazorProjectEngine projectEngine)
        => projectEngine.CreateCodeDocument(string.Empty, DefaultFileKind, importSources: default);

    public static RazorCodeDocument CreateEmptyDesignTimeCodeDocument(this RazorProjectEngine projectEngine)
        => projectEngine.CreateDesignTimeCodeDocument(string.Empty, DefaultFileKind, importSources: default);

    public static RazorCodeDocument CreateCodeDocument(this RazorProjectEngine projectEngine, string content)
        => projectEngine.CreateCodeDocument(content, DefaultFileKind, importSources: default);

    public static RazorCodeDocument CreateCodeDocument(this RazorProjectEngine projectEngine, string content, string fileKind)
        => projectEngine.CreateCodeDocument(content, fileKind, importSources: default);

    public static RazorCodeDocument CreateCodeDocument(
        this RazorProjectEngine projectEngine,
        string content,
        ImmutableArray<RazorSourceDocument> importSources)
        => projectEngine.CreateCodeDocument(content, fileKind: DefaultFileKind, importSources);

    public static RazorCodeDocument CreateCodeDocument(
        this RazorProjectEngine projectEngine,
        string content,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources)
    {
        var source = TestRazorSourceDocument.Create(content);
        return projectEngine.CreateCodeDocument(source, fileKind, importSources, tagHelpers: null, cssScope: null);
    }

    public static RazorCodeDocument CreateDesignTimeCodeDocument(this RazorProjectEngine projectEngine, string content)
        => projectEngine.CreateDesignTimeCodeDocument(content, DefaultFileKind, importSources: default);

    public static RazorCodeDocument CreateDesignTimeCodeDocument(this RazorProjectEngine projectEngine, string content, string fileKind)
        => projectEngine.CreateDesignTimeCodeDocument(content, fileKind, importSources: default);

    public static RazorCodeDocument CreateDesignTimeCodeDocument(
        this RazorProjectEngine projectEngine,
        string content,
        ImmutableArray<RazorSourceDocument> importSources)
        => projectEngine.CreateDesignTimeCodeDocument(content, fileKind: DefaultFileKind, importSources);

    public static RazorCodeDocument CreateDesignTimeCodeDocument(
        this RazorProjectEngine projectEngine,
        string content,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources)
    {
        var source = TestRazorSourceDocument.Create(content);
        return projectEngine.CreateDesignTimeCodeDocument(source, fileKind, importSources, tagHelpers: null);
    }
}
