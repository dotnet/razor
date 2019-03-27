// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components
{
    internal class ComponentMarkupEntityPass : ComponentIntermediateNodePassBase, IRazorOptimizationPass
    {
        // Runs after ComponentMarkupBlockPass
        public override int Order => 10010;

        protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            if (!IsComponentDocument(documentNode))
            {
                return;
            }

            if (documentNode.Options.DesignTime)
            {
                // Nothing to do during design time.
                return;
            }

            var rewriter = new Rewriter();
            rewriter.Visit(documentNode);
        }

        private class Rewriter : IntermediateNodeWalker
        {
            private static readonly char[] EncodedCharacters = new[] { '\r', '\n', '\t', '&' };

            public override void VisitHtml(HtmlContentIntermediateNode node)
            {
                var content = node.GetHtmlContent();
                for (var i = 0; i < content.Length; i++)
                {
                    var ch = content[i];
                    if (ch > 127 || EncodedCharacters.Contains(ch))
                    {
                        // Mark this content to denote they are already encoded.
                        node.SetEncoded();
                        break;
                    }
                }
            }
        }
    }
}
