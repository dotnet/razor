using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    static class RazorTextExtensions
    {
        public static CodeAnalysis.Text.TextSpan AsCodeAnalysisTextSpan(this Language.Syntax.TextSpan textSpan)
        {
            return new CodeAnalysis.Text.TextSpan(textSpan.Start, textSpan.Length);
        }
    }
}
