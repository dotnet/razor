// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorSyntaxTree
{
    internal SyntaxNode Root { get; }
    public RazorParserOptions Options { get; }
    public RazorSourceDocument Source { get; }

    private readonly IReadOnlyList<RazorDiagnostic> _diagnostics;
    private IReadOnlyList<RazorDiagnostic> _allDiagnostics;

    internal RazorSyntaxTree(
        SyntaxNode root,
        RazorSourceDocument source,
        IReadOnlyList<RazorDiagnostic> diagnostics,
        RazorParserOptions options)
    {
        Root = root;
        Source = source;
        _diagnostics = diagnostics;
        Options = options;
    }

    public IReadOnlyList<RazorDiagnostic> Diagnostics
    {
        get
        {
            if (_allDiagnostics == null)
            {
                var allDiagnostics = new HashSet<RazorDiagnostic>();
                for (var i = 0; i < _diagnostics.Count; i++)
                {
                    allDiagnostics.Add(_diagnostics[i]);
                }

                var rootDiagnostics = Root.GetAllDiagnostics();
                for (var i = 0; i < rootDiagnostics.Count; i++)
                {
                    allDiagnostics.Add(rootDiagnostics[i]);
                }

                var allOrderedDiagnostics = allDiagnostics.OrderBy(diagnostic => diagnostic.Span.AbsoluteIndex);
                _allDiagnostics = allOrderedDiagnostics.ToArray();
            }

            return _allDiagnostics;
        }
    }

    internal static RazorSyntaxTree Create(
        SyntaxNode root,
        RazorSourceDocument source,
        IEnumerable<RazorDiagnostic> diagnostics,
        RazorParserOptions options)
    {
        ArgHelper.ThrowIfNull(root);
        ArgHelper.ThrowIfNull(source);
        ArgHelper.ThrowIfNull(diagnostics);
        ArgHelper.ThrowIfNull(options);

        return new RazorSyntaxTree(root, source, new List<RazorDiagnostic>(diagnostics), options);
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
