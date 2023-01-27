// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Legacy;

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class DefaultRazorTagHelperRewritePhase : RazorEnginePhaseBase
{
    private static readonly ISet<TagHelperDescriptor> EmptyTagHelperSet = new HashSet<TagHelperDescriptor>();

    protected override void ExecuteCore(RazorCodeDocument codeDocument)
    {
        var syntaxTree = codeDocument.GetPreTagHelperSyntaxTree();
        var context = codeDocument.GetTagHelperContext();

        var rewrittenSyntaxTree = syntaxTree;
        var usedHelpers = EmptyTagHelperSet;

        if (syntaxTree is not null && context?.TagHelpers.Count > 0)
        {
            rewrittenSyntaxTree = TagHelperParseTreeRewriter.Rewrite(syntaxTree, context.Prefix, context.TagHelpers, out usedHelpers);
        }

        codeDocument.SetReferencedTagHelpers(usedHelpers);
        codeDocument.SetSyntaxTree(rewrittenSyntaxTree);
    }
}
