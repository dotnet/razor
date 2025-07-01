// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

internal static class CSharpCodeSnippets
{
    public const string True = "true";
    public const string False = "false";
    public const string Null = "null";
    public const string Async = "async";
    public const string HashLine = "#line";
    public const string Default = "default";

    public const string Bang = "!";
    public const string Semicolon = ";";
    public const string OpenBrace = "{";
    public const string CloseBrace = "}";
    public const string OpenParen = "(";
    public const string CloseParen = ")";
    public const string Space = " ";

    public const string EmptyQuotes = "\"\"";
    public const string DoubleQuote = "\"";
    public const string VerbatimDoubleQuote = "@\"";

    public const string Assignment = " = ";
    public const string LambdaArrow = " => ";
    public const string TypeListSeparator = " : ";
    public const string CommaSeparator = ", ";

    public static CodeSnippet AsSnippet(this string value)
        => Snippet(value);

    public static CodeSnippet Snippet(string value)
        => new(value);

    public static CodeSnippet Snippet(ref CodeSnippet.CodeSnippetInterpolatedStringHandler handler)
        => new(ref handler);

    public static CodeSnippet SeparatedList(CodeSnippet separater, IEnumerable<CodeSnippet> snippets)
    {
        using var builder = new PooledArrayBuilder<CodeSnippet>();

        var first = true;

        foreach (var snippet in snippets)
        {
            if (!first)
            {
                builder.Add(separater);
            }
            else
            {
                first = false;
            }

            builder.Add(snippet);
        }

        return new(in builder);
    }
}
