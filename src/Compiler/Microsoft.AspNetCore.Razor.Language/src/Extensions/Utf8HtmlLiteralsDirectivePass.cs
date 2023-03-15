// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public sealed class Utf8HtmlLiteralsDirectivePass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
{
    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        foreach (var utf8Directive in documentNode.FindDirectiveReferences(Utf8HtmlLiteralsDirective.Directive))
        {
            var token = ((DirectiveIntermediateNode)utf8Directive.Node).Tokens.FirstOrDefault();
            if (token != null)
            {
                // Page has opted into writing HTML literals as C# UTF8 string literals
                codeDocument.GetCodeGenerationOptions().WriteHtmlUtf8StringLiterals = true;
                break;
            }
        }
    }
}
