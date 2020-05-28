using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    static class RazorTextExtensions
    {
        public static CodeAnalysis.Text.TextSpan AsCodeAnalysisTextSpan(this Language.Syntax.TextSpan textSpan)
        {
            return new CodeAnalysis.Text.TextSpan(textSpan.Start, textSpan.Length);
        }

        public static Range AsRange(this SyntaxNode syntaxNode, RazorCodeDocument codeDocument)
        {
            var span = syntaxNode.GetSourceSpan(codeDocument.Source);
            var end = codeDocument.Source.Lines.GetLocation(span.AbsoluteIndex + span.Length + 1);
            return new Range(
                new Position(span.LineIndex, span.CharacterIndex),
                new Position(end.LineIndex, end.CharacterIndex));
        }
    }
}
