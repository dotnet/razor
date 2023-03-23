﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.Language;

internal class DirectiveTokenEditHandler : SpanEditHandler
{
    public static void SetupBuilder(SpanEditHandlerBuilder builder, AcceptedCharactersInternal acceptedCharacters, Func<string, IEnumerable<Syntax.InternalSyntax.SyntaxToken>> tokenizer)
    {
        builder.AcceptedCharacters = acceptedCharacters;
        builder.Factory = static (acceptedCharacters, tokenizer) => new DirectiveTokenEditHandler
        {
            AcceptedCharacters = acceptedCharacters,
            Tokenizer = tokenizer,
        };
    }

    protected override PartialParseResultInternal CanAcceptChange(SyntaxNode target, SourceChange change)
    {
        if (AcceptedCharacters == AcceptedCharactersInternal.NonWhitespace)
        {
            var originalText = change.GetOriginalText(target);
            var editedContent = change.GetEditedContent(target);

            if (!ContainsWhitespace(originalText) && !ContainsWhitespace(editedContent))
            {
                // Did not modify whitespace, directive format should be the same.
                // Return provisional so extensible IR/code gen pieces can see the full directive text
                // once the user stops editing the document.
                return PartialParseResultInternal.Accepted | PartialParseResultInternal.Provisional;
            }
        }

        return PartialParseResultInternal.Rejected;
    }

    private static bool ContainsWhitespace(string content)
    {
        for (var i = 0; i < content.Length; i++)
        {
            if (char.IsWhiteSpace(content[i]))
            {
                return true;
            }
        }

        return false;
    }
}
