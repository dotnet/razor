// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Extensions
{
    public sealed class FunctionsDirectivePass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
    {
        protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            var @class = documentNode.FindPrimaryClass();
            if (@class == null)
            {
                return;
            }

            foreach (var functions in documentNode.FindDirectiveReferences(FunctionsDirective.Directive))
            {
                for (var i = 0; i < functions.Node.Children.Count; i++)
                {
                    var child = functions.Node.Children[i];
                    if (child is TemplateIntermediateNode)
                    {
                        // If there's a template at the top level in the functions block we want to just 'inline'
                        // its content. In this context a template acts like a transition to markup.
                        @class.Children.AddRange(child.Children);
                        continue;
                    }

                    @class.Children.Add(child);
                }
            }
        }
    }
}
