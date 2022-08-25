// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language;

public static class TestRazorCodeDocument
{
    public static RazorCodeDocument CreateEmpty()
    {
        var source = TestRazorSourceDocument.Create(content: string.Empty);
        return new DefaultRazorCodeDocument(source, imports: null);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0027:Public API with optional parameter(s) should have the most parameters amongst its public overloads", Justification = "Test only")]
    public static RazorCodeDocument Create(string content, bool normalizeNewLines = false)
    {
        var source = TestRazorSourceDocument.Create(content, normalizeNewLines: normalizeNewLines);
        return new DefaultRazorCodeDocument(source, imports: null);
    }

    public static RazorCodeDocument Create(RazorSourceDocument source, IEnumerable<RazorSourceDocument> imports)
    {
        return new DefaultRazorCodeDocument(source, imports);
    }
}
