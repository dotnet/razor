﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

[DataContract]
internal readonly record struct RazorFormattingOptions
{
    [DataMember(Order = 0)]
    public bool InsertSpaces { get; init; } = true;
    [DataMember(Order = 1)]
    public int TabSize { get; init; } = 4;
    [DataMember(Order = 2)]
    public bool CodeBlockBraceOnNextLine { get; init; } = false;

    public RazorFormattingOptions()
    {
    }

    public static RazorFormattingOptions From(FormattingOptions options, bool codeBlockBraceOnNextLine)
    {
        return new RazorFormattingOptions()
        {
            InsertSpaces = options.InsertSpaces,
            TabSize = options.TabSize,
            CodeBlockBraceOnNextLine = codeBlockBraceOnNextLine
        };
    }

    public RazorIndentationOptions ToIndentationOptions()
        => new(
            UseTabs: !InsertSpaces,
            TabSize: TabSize,
            IndentationSize: TabSize);
}
