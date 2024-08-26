// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorSyntaxTree
{
    internal SyntaxNode Root { get; }
    public RazorParserOptions Options { get; }
    public RazorSourceDocument Source { get; }

    private readonly List<RazorDiagnostic> _diagnostics;
    private IReadOnlyList<RazorDiagnostic> _allDiagnostics;

    internal RazorSyntaxTree(
        SyntaxNode root,
        RazorSourceDocument source,
        IEnumerable<RazorDiagnostic> diagnostics,
        RazorParserOptions options)
    {
        ArgHelper.ThrowIfNull(root);
        ArgHelper.ThrowIfNull(source);
        ArgHelper.ThrowIfNull(diagnostics);
        ArgHelper.ThrowIfNull(options);

        Root = root;
        Source = source;
        _diagnostics = new List<RazorDiagnostic>(diagnostics);
        Options = options;
    }

    public IReadOnlyList<RazorDiagnostic> Diagnostics
    {
        get
        {
            if (_allDiagnostics == null)
            {
                using var allDiagnostics = new PooledArrayBuilder<RazorDiagnostic>();
                using var pooledList = ListPool<RazorDiagnostic>.GetPooledObject(out var rootDiagnostics);
                using var diagnosticSet = new PooledHashSet<RazorDiagnostic>();

                foreach (var diagnostic in _diagnostics)
                {
                    if (diagnosticSet.Add(diagnostic))
                    {
                        allDiagnostics.Add(diagnostic);
                    }
                }

                Root.CollectAllDiagnostics(rootDiagnostics);

                if (rootDiagnostics.Count > 0)
                {
                    foreach (var diagnostic in rootDiagnostics)
                    {
                        if (diagnosticSet.Add(diagnostic))
                        {
                            allDiagnostics.Add(diagnostic);
                        }
                    }
                }

                _allDiagnostics = diagnosticSet.OrderByAsArray(static d => d.Span.AbsoluteIndex);
            }

            return _allDiagnostics;
        }
    }

    public static RazorSyntaxTree Parse(RazorSourceDocument source)
    {
        ArgHelper.ThrowIfNull(source);

        return Parse(source, options: null);
    }

    public static RazorSyntaxTree Parse(RazorSourceDocument source, RazorParserOptions options)
    {
        ArgHelper.ThrowIfNull(source);

        var parser = new RazorParser(options ?? RazorParserOptions.CreateDefault());
        return parser.Parse(source);
    }
}
