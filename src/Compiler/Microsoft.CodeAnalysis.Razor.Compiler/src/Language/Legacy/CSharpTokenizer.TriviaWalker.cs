using SyntaxToken = Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax.SyntaxToken;
using SyntaxFactory = Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax.SyntaxFactory;
using CSharpSyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using CSharpSyntaxToken = Microsoft.CodeAnalysis.SyntaxToken;
using CSharpSyntaxTriviaList = Microsoft.CodeAnalysis.SyntaxTriviaList;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal partial class CSharpTokenizer
{
    private struct SyntaxTriviaWalker(CSharpSyntaxTriviaList triviaList)
    {
        private int _index;

    }
}
