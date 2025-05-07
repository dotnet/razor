// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

internal interface IEndTagSyntaxNode
{
    SyntaxNode? Parent { get; }
    TextSpan Span { get; }
    int SpanStart { get; }

    SyntaxToken OpenAngle { get; }
    SyntaxToken? ForwardSlash { get; }
    SyntaxToken? Bang { get; }
    SyntaxToken Name { get; }
    MarkupMiscAttributeContentSyntax? MiscAttributeContent { get; }
    SyntaxToken CloseAngle { get; }
    ISpanChunkGenerator? ChunkGenerator { get; }
}
