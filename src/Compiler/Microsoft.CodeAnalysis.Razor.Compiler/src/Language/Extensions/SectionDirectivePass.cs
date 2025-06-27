// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


#nullable disable

using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public sealed class SectionDirectivePass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
{
    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        var @class = documentNode.FindPrimaryClass();
        if (@class == null)
        {
            return;
        }

        foreach (var directive in documentNode.FindDirectiveReferences(SectionDirective.Directive))
        {
            var directiveNode = directive.Node;
            var sectionName = directiveNode.Tokens.FirstOrDefault()?.Content;

            var section = new SectionIntermediateNode()
            {
                SectionName = sectionName,
            };

            var i = 0;
            for (; i < directiveNode.Children.Count; i++)
            {
                if (directiveNode.Children[i] is not DirectiveTokenIntermediateNode)
                {
                    break;
                }
            }

            while (i != directiveNode.Children.Count)
            {
                // Move non-token children over to the section node so we don't have double references to children nodes.
                section.Children.Add(directiveNode.Children[i]);
                directiveNode.Children.RemoveAt(i);
            }

            directive.InsertAfter(section);
        }
    }
}
