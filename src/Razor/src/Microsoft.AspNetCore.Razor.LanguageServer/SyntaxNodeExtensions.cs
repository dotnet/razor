// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public static class SyntaxNodeExtensions
    {
        internal static Range GetRange(this SyntaxNode syntaxNode, RazorCodeDocument codeDocument)
        {
            if (syntaxNode is null)
            {
                throw new ArgumentNullException(nameof(syntaxNode));
            }

            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            try
            {
                int startPosition;
                int endPosition;
                if (syntaxNode is MarkupTagHelperAttributeSyntax thAttributeSyntax)
                {
                    startPosition = thAttributeSyntax.Name.Position;
                    endPosition = thAttributeSyntax.Name.EndPosition;
                }
                else if (syntaxNode is MarkupMinimizedTagHelperAttributeSyntax thAttrSyntax)
                {
                    startPosition = thAttrSyntax.Name.Position;
                    endPosition = thAttrSyntax.Name.EndPosition;
                }
                else
                {
                    startPosition = syntaxNode.Position;
                    endPosition = syntaxNode.EndPosition;
                }
                var startLocation = codeDocument.Source.Lines.GetLocation(startPosition);
                var endLocation = codeDocument.Source.Lines.GetLocation(endPosition);

                return new Range
                {
                    Start = new Position(startLocation.LineIndex, startLocation.CharacterIndex),
                    End = new Position(endLocation.LineIndex, endLocation.CharacterIndex)
                };
            }
            catch (IndexOutOfRangeException)
            {
                Debug.Assert(false, "Node position should stay within document length.");
                return null;
            }
        }
    }
}
