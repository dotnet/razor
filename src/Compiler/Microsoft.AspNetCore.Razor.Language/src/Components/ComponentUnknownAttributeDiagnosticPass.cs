// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
            var hasCaptureUnmatchedValues = false;
            var boundComponentAttributes = component.BoundAttributes;
            for (var i = 0; i < boundComponentAttributes.Count; i++)
            {
                var attribute = boundComponentAttributes[i];
                if (attribute.Metadata.TryGetValue(ComponentMetadata.Component.CaptureUnmatchedValues, out var captureUnmatchedValues))
                {
                    hasCaptureUnmatchedValues = captureUnmatchedValues == "True";
                    break;
                }
            }


            // If no arbitrary attributes can be accepted by the component, check if all
            // the user-specified attribute names map to an underlying property
            if (!hasCaptureUnmatchedValues)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (node.Children[i] is ComponentAttributeIntermediateNode attribute &&
                        attribute.AttributeName != null)
                    {
                        if (attribute.BoundAttribute == null)
                        {
                            attribute.Diagnostics.Add(ComponentDiagnosticFactory.Create_UnknownMarkupAttribute(
                                attribute.AttributeName, attribute.Source));
                        }
                    }
                }
            }

            base.VisitComponent(node);
        }
    }
}
