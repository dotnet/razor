// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal class ComponentChildContentDiagnosticPass : ComponentIntermediateNodePassBase, IRazorOptimizationPass
{
    // Runs after components/eventhandlers/ref/bind/templates. We want to validate every component
    // and it's usage of ChildContent.
    public override int Order => 160;

    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        if (!IsComponentDocument(documentNode))
        {
            return;
        }

        var visitor = new Visitor();
        visitor.Visit(documentNode);
    }

    private class Visitor : IntermediateNodeWalker
    {
        public override void VisitComponent(ComponentIntermediateNode node)
        {
            // Check for properties that are set by both element contents (body) and the attribute itself.
            foreach (var childContent in node.ChildContents)
            {
                foreach (var attribute in node.Attributes)
                {
                    if (attribute.AttributeName == childContent.AttributeName)
                    {
                        node.AddDiagnostic(ComponentDiagnosticFactory.Create_ChildContentSetByAttributeAndBody(
                            attribute.Source,
                            attribute.AttributeName));
                    }
                }
            }

            base.VisitDefault(node);
        }

        public override void VisitComponentChildContent(ComponentChildContentIntermediateNode node)
        {
            // Check that each child content has a unique parameter name within its scope. This is important
            // because the parameter name can be implicit, and it doesn't work well when nested.
            if (node.IsParameterized)
            {
                var ancestors = Ancestors;

                for (var i = 0; i < ancestors.Length - 1; i++)
                {
                    if (ancestors[i] is ComponentChildContentIntermediateNode ancestor &&
                        ancestor.IsParameterized &&
                        string.Equals(node.ParameterName, ancestor.ParameterName, StringComparison.Ordinal))
                    {
                        // Duplicate name. We report an error because this will almost certainly also lead to an error
                        // from the C# compiler that's way less clear.
                        node.AddDiagnostic(ComponentDiagnosticFactory.Create_ChildContentRepeatedParameterName(
                            node.Source,
                            node,
                            (ComponentIntermediateNode)ancestors[0], // Enclosing component
                            ancestor, // conflicting child content node
                            (ComponentIntermediateNode)ancestors[i + 1]));  // Enclosing component of conflicting child content node
                    }
                }
            }

            base.VisitDefault(node);
        }
    }
}
