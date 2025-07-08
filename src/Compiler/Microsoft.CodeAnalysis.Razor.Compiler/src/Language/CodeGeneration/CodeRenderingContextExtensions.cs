// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using static Microsoft.AspNetCore.Razor.Language.CodeGeneration.CodeWriterExtensions;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

internal static class CodeRenderingContextExtensions
{
    public static CSharpCodeWritingScope BuildNamespace(this CodeRenderingContext context, string? name, SourceSpan? span)
    {
        var writer = context.CodeWriter;

        if (name.IsNullOrEmpty())
        {
            return new CSharpCodeWritingScope(writer, writeBraces: false);
        }

        writer.Write("namespace ");

        if (context.Options.DesignTime || span is null)
        {
            writer.WriteLine(name);
        }
        else
        {
            writer.WriteLine();
            using (writer.BuildEnhancedLinePragma(span, context))
            {
                writer.WriteLine(name);
            }
        }
        return new CSharpCodeWritingScope(writer);
    }
}
