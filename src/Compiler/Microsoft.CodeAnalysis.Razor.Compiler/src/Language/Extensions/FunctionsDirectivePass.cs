// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public sealed class FunctionsDirectivePass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
{
    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        var @class = documentNode.FindPrimaryClass();
        if (@class == null)
        {
            return;
        }

        using var _ = ArrayBuilderPool<IntermediateNodeReference<DirectiveIntermediateNode>>.GetPooledObject(out var directiveReferences);

        directiveReferences.AddRange(documentNode.FindDirectiveReferences(FunctionsDirective.Directive));

        if (codeDocument.FileKind.IsComponent())
        {
            directiveReferences.AddRange(documentNode.FindDirectiveReferences(ComponentCodeDirective.Directive));
        }

        // Now we have all the directive nodes, we want to add them to the end of the class node in document order.
        var orderedDirectives = directiveReferences.ToImmutableOrderedByAndClear(static n => n.Node.Source?.AbsoluteIndex);

        foreach (var directiveReference in orderedDirectives)
        {
            var node = directiveReference.Node;
            @class.Children.AddRange(node.Children);

            // We don't want to keep the original directive node around anymore.
            // Otherwise this can cause unintended side effects in the subsequent passes.
            directiveReference.Remove();
        }
    }
}
