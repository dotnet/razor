using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    abstract class RazorSyntaxService
    {
        public SyntaxNode GetNode(RazorCodeDocument codeDocument, SourceLocation location)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var syntaxTree = codeDocument.GetSyntaxTree();
            return RazorSyntaxExtensions.GetNodeFromLocation(syntaxTree.Root, location);
        }
    }
}
