// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract class RazorCSharpDocument : IRazorGeneratedDocument
{
    public abstract string GeneratedCode { get; }

    public abstract ImmutableArray<SourceMapping> SourceMappings { get; }

    /// <summary>
    /// Maps spans from the generated source to the whole component.
    /// </summary>
    /// <remarks>
    /// Used to map the component class name, so go-to-definition navigates to the .razor file.
    /// </remarks>
    public abstract ImmutableArray<SourceSpan> ComponentMappings { get; }

    public abstract IReadOnlyList<RazorDiagnostic> Diagnostics { get; }

    public abstract RazorCodeGenerationOptions Options { get; }

    public abstract RazorCodeDocument CodeDocument { get; }

    internal virtual IReadOnlyList<LinePragma> LinePragmas { get; }

    [Obsolete("For backwards compatibility only. Use the overload that takes a RazorCodeDocument")]
    public static RazorCSharpDocument Create(string generatedCode, RazorCodeGenerationOptions options, IEnumerable<RazorDiagnostic> diagnostics)
        => Create(codeDocument: null, generatedCode, options, diagnostics);

    [Obsolete("For backwards compatibility only. Use the overload that takes a RazorCodeDocument")]
    public static RazorCSharpDocument Create(string generatedCode, RazorCodeGenerationOptions options, IEnumerable<RazorDiagnostic> diagnostics, ImmutableArray<SourceMapping> sourceMappings, IEnumerable<LinePragma> linePragmas)
        => Create(codeDocument: null, generatedCode, options, diagnostics, sourceMappings, ImmutableArray<SourceSpan>.Empty, linePragmas);

    public static RazorCSharpDocument Create(RazorCodeDocument codeDocument, string generatedCode, RazorCodeGenerationOptions options, IEnumerable<RazorDiagnostic> diagnostics)
    {
        if (generatedCode == null)
        {
            throw new ArgumentNullException(nameof(generatedCode));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (diagnostics == null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        return new DefaultRazorCSharpDocument(codeDocument, generatedCode, options, diagnostics.ToArray(), sourceMappings: ImmutableArray<SourceMapping>.Empty, componentMappings: ImmutableArray<SourceSpan>.Empty, linePragmas: null);
    }

    public static RazorCSharpDocument Create(
        RazorCodeDocument codeDocument,
        string generatedCode,
        RazorCodeGenerationOptions options,
        IEnumerable<RazorDiagnostic> diagnostics,
        ImmutableArray<SourceMapping> sourceMappings,
        ImmutableArray<SourceSpan> componentMappings,
        IEnumerable<LinePragma> linePragmas)
    {
        if (generatedCode == null)
        {
            throw new ArgumentNullException(nameof(generatedCode));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (diagnostics == null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        return new DefaultRazorCSharpDocument(codeDocument, generatedCode, options, diagnostics.ToArray(), sourceMappings, componentMappings, linePragmas.ToArray());
    }
}
