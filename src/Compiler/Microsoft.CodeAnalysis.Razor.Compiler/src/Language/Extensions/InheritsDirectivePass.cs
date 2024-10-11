// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


#nullable disable

using System.Linq;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public sealed class InheritsDirectivePass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
{
    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        var @class = documentNode.FindPrimaryClass();
        if (@class == null)
        {
            return;
        }

        foreach (var inherits in documentNode.FindDirectiveReferences(InheritsDirective.Directive))
        {
            var token = ((DirectiveIntermediateNode)inherits.Node).Tokens.FirstOrDefault();
            if (token != null)
            {
                var source = codeDocument.GetParserOptions()?.DesignTime == true ? null : token.Source;

                // Only MVC and razor documents support also updating the base type when setting the model
                @class.BaseType = documentNode.DocumentKind == MvcViewDocumentClassifierPass.MvcViewDocumentKind || documentNode.DocumentKind == RazorPageDocumentClassifierPass.RazorPageDocumentKind
                                ? new BaseTypeIntermediateNode(token.Content, source)
                                : IntermediateToken.CreateCSharpToken(token.Content, source);
                break;
            }
        }
    }
}
