// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;
internal sealed class ComponentUnknownAttributeDiagnosticPass : ComponentIntermediateNodePassBase, IRazorOptimizationPass
{
    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        var visitor = new Visitor();
        visitor.Visit(documentNode);
    }

    private class Visitor : IntermediateNodeWalker
    {
        public override void VisitComponent(ComponentIntermediateNode node)
        {
            // First, check if there is a property of type 'IDictionary<string, object>'
            // with 'CaptureUnmatchedValues' set to 'true'
            var component = node.Component;
            var boundComponentAttributes = component.BoundAttributes;
            for (var i = 0; i < boundComponentAttributes.Count; i++)
            {
                var attribute = boundComponentAttributes[i];
                // [HELP NEEDED] I would need to access component Type information here in order to check for CaptureUnmatchedValues
            }


            // If no arbitrary attributes can be accepted by the component, check if all
            // the user-specified attribute names map to an underlying property
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is ComponentAttributeIntermediateNode attribute && attribute.AttributeName != null)
                {
                    if (attribute.BoundAttribute == null)
                    {
                        attribute.Diagnostics.Add(ComponentDiagnosticFactory.Create_UnknownMarkupAttribute(
                            attribute.AttributeName, attribute.Source));
                    }
                }
            }

            base.VisitComponent(node);
        }
    }
}
