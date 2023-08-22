// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal sealed class ComponentRenderModeLoweringPass : ComponentIntermediateNodePassBase, IRazorOptimizationPass
{
    // Run after component lowering pass
    public override int Order => 50;

    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        if (!IsComponentDocument(documentNode))
        {
            return;
        }

        var references = documentNode.FindDescendantReferences<TagHelperDirectiveAttributeIntermediateNode>();
        foreach (var reference in references)
        {
            if (reference is { Node: TagHelperDirectiveAttributeIntermediateNode node, Parent: IntermediateNode parentNode } && node.TagHelper.IsRenderModeTagHelper())
            {
                Debug.Assert(node.Diagnostics.Count == 0);
                if (parentNode is ComponentIntermediateNode)
                {
                    var expression = node.Children[0] switch
                    {
                        CSharpExpressionIntermediateNode cSharpNode => cSharpNode.Children[0],
                        IntermediateNode token => token
                    };

                    reference.Replace(new RenderModeIntermediateNode(expression));
                }
                else
                {
                    // This is on a regular HTML element, not a component, so lower it to a regular HTML attribute
                    var attributeNode = new HtmlAttributeIntermediateNode()
                    {
                        Source = node.Source,
                        AttributeName = node.OriginalAttributeName,
                        Children =
                        {
                            node.Children[0] switch
                            {
                                CSharpExpressionIntermediateNode cSharpNode => new CSharpExpressionAttributeValueIntermediateNode()
                                {
                                    Source = node.Source,
                                    Children = { cSharpNode.Children[0] }
                                },
                                IntermediateNode token => new HtmlAttributeValueIntermediateNode()
                                {
                                    Source = node.Source,
                                    Children = { token }
                                }
                            }
                        }
                    };
                    attributeNode.Diagnostics.AddRange(node.Diagnostics);
                    reference.Replace(attributeNode);
                }
            }
        }
    }
}
