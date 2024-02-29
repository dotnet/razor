﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal class TagHelperSpanWriter(TextWriter writer, RazorSyntaxTree syntaxTree)
{
    private readonly RazorSyntaxTree _syntaxTree = syntaxTree;
    private readonly TextWriter _writer = writer;

    public virtual void Visit()
    {
        var tagHelperSpans = _syntaxTree.GetTagHelperSpans();
        foreach (var span in tagHelperSpans)
        {
            VisitTagHelperSpan(span);
            WriteNewLine();
        }
    }

    public virtual void VisitTagHelperSpan(TagHelperSpanInternal span)
    {
        WriteTagHelperSpan(span);
    }

    protected void WriteTagHelperSpan(TagHelperSpanInternal span)
    {
        Write($"TagHelper span at {span.Span}");
        foreach (var tagHelper in span.TagHelpers)
        {
            WriteSeparator();

            // Get the type name without the namespace.
            var typeName = tagHelper.Name[(tagHelper.Name.LastIndexOf('.') + 1)..];
            Write(typeName);
        }
    }

    protected void WriteSeparator()
    {
        Write(" - ");
    }

    protected void WriteNewLine()
    {
        _writer.WriteLine();
    }

    protected void Write(object value)
    {
        _writer.Write(value);
    }
}
