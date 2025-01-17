// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class RazorHtmlDocument : IRazorGeneratedDocument
{
    public RazorCodeDocument CodeDocument { get; }
    public string GeneratedCode { get; }
    public RazorCodeGenerationOptions Options { get; }
    public ImmutableArray<SourceMapping> SourceMappings { get; }

    public RazorHtmlDocument(
        RazorCodeDocument codeDocument,
        string generatedCode,
        RazorCodeGenerationOptions options,
        ImmutableArray<SourceMapping> sourceMappings = default)
    {
        ArgHelper.ThrowIfNull(codeDocument);
        ArgHelper.ThrowIfNull(generatedCode);
        ArgHelper.ThrowIfNull(options);

        CodeDocument = codeDocument;
        GeneratedCode = generatedCode;
        Options = options;
        SourceMappings = sourceMappings.NullToEmpty();
    }
}
