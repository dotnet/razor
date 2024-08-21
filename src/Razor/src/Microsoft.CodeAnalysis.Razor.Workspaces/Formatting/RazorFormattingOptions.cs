// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

internal sealed record RazorFormattingOptions
{
    public bool InsertSpaces { get; init; }
    public int TabSize { get; init; }
    public bool CodeBlockBraceOnNextLine { get; init; }

    public static RazorFormattingOptions Default => new RazorFormattingOptions()
    {
        InsertSpaces = true,
        TabSize = 4,
        CodeBlockBraceOnNextLine = false
    };

    public static RazorFormattingOptions From(FormattingOptions options, bool codeBlockBraceOnNextLine)
    {
        return new RazorFormattingOptions()
        {
            InsertSpaces = options.InsertSpaces,
            TabSize = options.TabSize,
            CodeBlockBraceOnNextLine = codeBlockBraceOnNextLine
        };
    }

    public RazorIndentationOptions GetIndentationOptions()
        => new(
            UseTabs: !InsertSpaces,
            TabSize: TabSize,
            IndentationSize: TabSize);
}
