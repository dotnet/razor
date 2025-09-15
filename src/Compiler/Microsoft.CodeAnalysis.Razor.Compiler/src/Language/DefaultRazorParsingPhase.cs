// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
#endif

using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

internal class DefaultRazorParsingPhase : RazorEnginePhaseBase, IRazorParsingPhase
{
    private static readonly ConditionalWeakTable<RazorSourceDocument, RazorSyntaxTree> s_importTrees = new();

    protected override void ExecuteCore(RazorCodeDocument codeDocument, CancellationToken cancellationToken)
    {
        var options = codeDocument.ParserOptions;
        var syntaxTree = RazorSyntaxTree.Parse(codeDocument.Source, options);
        codeDocument.SetSyntaxTree(syntaxTree);

        using var importSyntaxTrees = new PooledArrayBuilder<RazorSyntaxTree>(codeDocument.Imports.Length);

        foreach (var import in codeDocument.Imports)
        {
            if (!s_importTrees.TryGetValue(import, out var tree)
                || !tree.Options.Equals(options))
            {
                tree = RazorSyntaxTree.Parse(import, options);

#if NET
                s_importTrees.AddOrUpdate(import, tree);
#else
                try
                {
                    // good effort update of CWT value
                    s_importTrees.Remove(import);
                    s_importTrees.Add(import, tree);
                }
                catch (ArgumentException)
                {
                }
#endif
            }

            importSyntaxTrees.Add(tree);
        }

        codeDocument.SetImportSyntaxTrees(importSyntaxTrees.ToImmutableAndClear());
    }
}
