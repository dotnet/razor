// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis;

public static class RazorDiagnosticExtensions
{
    /// <summary>
    /// Provides better experience when verifying diagnostics in tests - includes squiggled text and code snippet.
    /// </summary>
    public static IEnumerable<Diagnostic> PretendTheseAreCSharpDiagnostics(
        this IEnumerable<Diagnostic> diagnostics,
        Func<string, SourceText> filePathToContent)
    {
        var texts = new Dictionary<string, SourceText>();
        return diagnostics.Select(d =>
        {
            if (d.Location is { Kind: LocationKind.ExternalFile } originalLocation)
            {
                var mappedSpan = originalLocation.GetMappedLineSpan();
                var path = mappedSpan.Path;
                var text = texts.GetOrAdd(path, filePathToContent);
                var syntaxTree = CSharpSyntaxTree.ParseText(text, path: path);
                var span = text.Lines.GetTextSpan(mappedSpan.Span);
                var location = Location.Create(syntaxTree, span);
                return Diagnostic.Create(d.Descriptor, location);
            }

            return d;
        });
    }
}
