// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

public static class RazorProjectEngineExtensions
{
    private static string DefaultFileKind => FileKinds.Legacy;

    public static RazorCodeDocument CreateEmptyCodeDocument(this RazorProjectEngine projectEngine)
    {
        var source = TestRazorSourceDocument.Create(content: string.Empty);

        return projectEngine.CreateCodeDocument(source, DefaultFileKind, importSources: default);
    }
}
