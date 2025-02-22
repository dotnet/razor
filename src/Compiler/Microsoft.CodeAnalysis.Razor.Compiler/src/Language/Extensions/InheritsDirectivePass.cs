﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


#nullable disable

using System.Linq;
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

        foreach (var inherits in documentNode.FindDirectiveReferences(InheritsDirective.Directive, includeMalformed: codeDocument.GetParserOptions()?.DesignTime != true))
        {
            var token = inherits.Node.Children.OfType<DirectiveTokenIntermediateNode>().FirstOrDefault();
            if (token != null)
            {
                var source = codeDocument.GetParserOptions()?.DesignTime == true ? null : token.Source;
                @class.BaseType = new BaseTypeWithModel(token.Content, source);
                break;
            }
        }
    }
}
