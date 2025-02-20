// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

public static class TestRazorCodeDocument
{
    public static RazorCodeDocument CreateEmpty()
        => RazorCodeDocument.Create(
            source: TestRazorSourceDocument.Create(content: string.Empty),
            imports: default,
            parserOptions: RazorParserOptions.Default,
            codeGenerationOptions: RazorCodeGenerationOptions.Default);

    public static RazorCodeDocument Create(string content, bool normalizeNewLines = false)
        => RazorCodeDocument.Create(
            source: TestRazorSourceDocument.Create(content, normalizeNewLines: normalizeNewLines),
            imports: default,
            parserOptions: RazorParserOptions.Default,
            codeGenerationOptions: RazorCodeGenerationOptions.Default);
}
