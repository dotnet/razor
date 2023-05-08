// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Legacy;

namespace Microsoft.AspNetCore.Razor.Language;
internal sealed class DefaultRazorTagHelperRewritePhase : RazorEnginePhaseBase
{
    protected override void ExecuteCore(RazorCodeDocument codeDocument)
    {
        var context = codeDocument.GetTagHelperContext();
        if (context is not null)
        {
            RewriteTagHelpers(codeDocument, context.Prefix, context.TagHelpers);
        }
    }

    public void RewriteTagHelpers(RazorCodeDocument codeDocument, string prefix, IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        var syntaxTree = codeDocument.GetPreTagHelperSyntaxTree() ?? codeDocument.GetSyntaxTree();
        ThrowForMissingDocumentDependency(syntaxTree);

        if (tagHelpers.Count > 0)
        {
            var rewrittenSyntaxTree = TagHelperParseTreeRewriter.Rewrite(syntaxTree, prefix, tagHelpers, out var usedHelpers, out var rewrittenMap);
            codeDocument.SetSyntaxTree(rewrittenSyntaxTree);
            codeDocument.SetReferencedTagHelpers(usedHelpers);
            codeDocument.SetRewrittenTagHelpers(rewrittenMap);
        }
        else
        {
            codeDocument.SetReferencedTagHelpers(new HashSet<TagHelperDescriptor>());
            codeDocument.SetRewrittenTagHelpers(new Dictionary<Syntax.MarkupStartTagSyntax, TagHelperBinding>());
        }
    }
}
