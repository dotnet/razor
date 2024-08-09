// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorCSharpDocument : IRazorGeneratedDocument
{
    public RazorCodeDocument CodeDocument { get; }
    public string GeneratedCode { get; }
    public RazorCodeGenerationOptions Options { get; }
    public IReadOnlyList<RazorDiagnostic> Diagnostics { get; }
    public ImmutableArray<SourceMapping> SourceMappings { get; }
    internal IReadOnlyList<LinePragma> LinePragmas { get; }

    public RazorCSharpDocument(
        RazorCodeDocument codeDocument,
        string generatedCode,
        RazorCodeGenerationOptions options,
        RazorDiagnostic[] diagnostics,
        ImmutableArray<SourceMapping> sourceMappings,
        LinePragma[] linePragmas)
    {
        ArgHelper.ThrowIfNull(codeDocument);
        ArgHelper.ThrowIfNull(generatedCode);

        CodeDocument = codeDocument;
        GeneratedCode = generatedCode;
        Options = options;

        Diagnostics = diagnostics ?? [];
        SourceMappings = sourceMappings;
        LinePragmas = linePragmas ?? [];
    }

    public static RazorCSharpDocument Create(
        RazorCodeDocument codeDocument,
        string generatedCode,
        RazorCodeGenerationOptions options,
        IEnumerable<RazorDiagnostic> diagnostics)
    {
        ArgHelper.ThrowIfNull(generatedCode);
        ArgHelper.ThrowIfNull(options);
        ArgHelper.ThrowIfNull(diagnostics);

        return new(codeDocument, generatedCode, options, diagnostics.ToArray(), sourceMappings: [], linePragmas: []);
    }

    public static RazorCSharpDocument Create(
        RazorCodeDocument codeDocument,
        string generatedCode,
        RazorCodeGenerationOptions options,
        IEnumerable<RazorDiagnostic> diagnostics,
        ImmutableArray<SourceMapping> sourceMappings,
        IEnumerable<LinePragma> linePragmas)
    {
        ArgHelper.ThrowIfNull(generatedCode);
        ArgHelper.ThrowIfNull(options);
        ArgHelper.ThrowIfNull(diagnostics);

        return new(codeDocument, generatedCode, options, diagnostics.ToArray(), sourceMappings, linePragmas.ToArray());
    }
}
