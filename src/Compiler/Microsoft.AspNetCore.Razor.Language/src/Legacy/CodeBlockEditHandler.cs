﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal class CodeBlockEditHandler : SpanEditHandler
{
    public static void SetupBuilder(SpanEditHandlerBuilder builder, Func<string, IEnumerable<Syntax.InternalSyntax.SyntaxToken>> tokenizer)
    {
        builder.Factory = (acceptedCharacters, tokenizer) => new CodeBlockEditHandler
        {
            AcceptedCharacters = acceptedCharacters,
            Tokenizer = tokenizer,
        };
    }

    protected override PartialParseResultInternal CanAcceptChange(SyntaxNode target, SourceChange change)
    {
        if (IsAcceptableDeletion(target, change))
        {
            return PartialParseResultInternal.Accepted;
        }

        if (IsAcceptableReplacement(target, change))
        {
            return PartialParseResultInternal.Accepted;
        }

        if (IsAcceptableInsertion(change))
        {
            return PartialParseResultInternal.Accepted;
        }

        return PartialParseResultInternal.Rejected;
    }

    // Internal for testing
    internal static bool IsAcceptableReplacement(SyntaxNode target, SourceChange change)
    {
        if (!change.IsReplace)
        {
            return false;
        }

        if (ContainsInvalidContent(change))
        {
            return false;
        }

        if (ModifiesInvalidContent(target, change))
        {
            return false;
        }

        return true;
    }

    // Internal for testing
    internal static bool IsAcceptableDeletion(SyntaxNode target, SourceChange change)
    {
        if (!change.IsDelete)
        {
            return false;
        }

        if (ModifiesInvalidContent(target, change))
        {
            return false;
        }

        return true;
    }

    // Internal for testing
    internal static bool ModifiesInvalidContent(SyntaxNode target, SourceChange change)
    {
        var relativePosition = change.Span.AbsoluteIndex - target.Position;

        if (target.GetContent().IndexOfAny(new[] { '{', '}', '@', '<', '*', }, relativePosition, change.Span.Length) >= 0)
        {
            return true;
        }

        return false;
    }

    // Internal for testing
    internal static bool IsAcceptableInsertion(SourceChange change)
    {
        if (!change.IsInsert)
        {
            return false;
        }

        if (ContainsInvalidContent(change))
        {
            return false;
        }

        return true;
    }

    // Internal for testing
    internal static bool ContainsInvalidContent(SourceChange change)
    {
        if (change.NewText.IndexOfAny(new[] { '{', '}', '@', '<', '*', }) >= 0)
        {
            return true;
        }

        return false;
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0};CodeBlock", base.ToString());
    }

    public override bool Equals(object obj)
    {
        return obj is CodeBlockEditHandler other &&
            base.Equals(other);
    }

    public override int GetHashCode() => base.GetHashCode();
}
