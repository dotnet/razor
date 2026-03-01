// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public sealed class Utf8HtmlLiteralsDirectivePass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
{
    protected override void ExecuteCore(
        RazorCodeDocument codeDocument,
        DocumentIntermediateNode documentNode,
        CancellationToken cancellationToken)
    {
        foreach (var directive in documentNode.FindDirectiveReferences(Utf8HtmlLiteralsDirective.Directive))
        {
            var token = directive.Node.Tokens.FirstOrDefault();
            if (token != null &&
                string.Equals(token.Content, "true", System.StringComparison.Ordinal))
            {
                documentNode.Options = documentNode.Options.WithFlags(writeHtmlUtf8StringLiterals: true);
                break;
            }
        }
    }
}
