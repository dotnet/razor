// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

public static class RazorProjectEngineExtensions
{
    private static string DefaultFileKind => FileKinds.Legacy;

    public static RazorCodeDocument CreateCodeDocument(
        this RazorProjectEngine projectEngine,
        RazorSourceDocument source)
    {
        return projectEngine.CreateCodeDocument(source, DefaultFileKind, importSources: default);
    }

    public static RazorCodeDocument CreateCodeDocument(
        this RazorProjectEngine projectEngine,
        RazorSourceDocument source,
        string fileKind)
    {
        return projectEngine.CreateCodeDocument(source, fileKind, importSources: default);
    }

    public static RazorCodeDocument CreateCodeDocument(
        this RazorProjectEngine projectEngine,
        RazorSourceDocument source,
        ImmutableArray<RazorSourceDocument> importSources)
    {
        return projectEngine.CreateCodeDocument(source, fileKind: DefaultFileKind, importSources);
    }

    public static RazorCodeDocument CreateCodeDocument(
        this RazorProjectEngine projectEngine,
        RazorSourceDocument source,
        string fileKind,
        ImmutableArray<RazorSourceDocument> importSources)
    {
        return projectEngine.CreateCodeDocument(source, fileKind, importSources, tagHelpers: null, cssScope: null);
    }
}
