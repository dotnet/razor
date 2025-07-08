// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
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

    public static CSharpCodeWritingScope BuildClassDeclaration(
        this CodeRenderingContext context,
        ImmutableArray<string> modifiers,
        string name,
        BaseTypeWithModel? baseType,
        ImmutableArray<IntermediateToken> interfaces,
        ImmutableArray<TypeParameter> typeParameters,
        bool useNullableContext = false)
    {
        var writer = context.CodeWriter;

        if (useNullableContext)
        {
            writer.WriteLine("#nullable restore");
        }

        foreach (var modifier in modifiers)
        {
            writer.Write(modifier);
            writer.Write(" ");
        }

        writer.Write("class ");
        writer.Write(name);

        if (!typeParameters.IsDefaultOrEmpty)
        {
            writer.Write("<");

            var first = true;

            foreach (var typeParameter in typeParameters)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    writer.Write(",");
                }

                if (typeParameter.ParameterNameSource is { } source)
                {
                    WriteWithPragma(writer, typeParameter.ParameterName, context, source);
                }
                else
                {
                    writer.Write((string)typeParameter.ParameterName);
                }
            }

            writer.Write(">");
        }

        var hasBaseType = !string.IsNullOrWhiteSpace(baseType?.BaseType.Content);
        var hasInterfaces = !interfaces.IsDefaultOrEmpty;

        if (hasBaseType || hasInterfaces)
        {
            writer.Write(" : ");

            if (hasBaseType)
            {
                Debug.Assert(baseType != null);

                WriteToken(baseType.BaseType);
                WriteOptionalToken(baseType.GreaterThan);
                WriteOptionalToken(baseType.ModelType);
                WriteOptionalToken(baseType.LessThan);

                if (hasInterfaces)
                {
                    writer.WriteParameterSeparator();
                }
            }

            if (hasInterfaces)
            {
                WriteToken(interfaces[0]);

                for (var i = 1; i < interfaces.Length; i++)
                {
                    writer.Write(", ");
                    WriteToken(interfaces[i]);
                }
            }
        }

        writer.WriteLine();
        if (typeParameters != null)
        {
            foreach (var typeParameter in typeParameters)
            {
                var constraint = typeParameter.Constraints;
                if (constraint != null)
                {
                    if (typeParameter.ConstraintsSource is { } source)
                    {
                        Debug.Assert(context != null);
                        WriteWithPragma(writer, constraint, context, source);
                    }
                    else
                    {
                        writer.Write(constraint);
                        writer.WriteLine();
                    }
                }
            }
        }

        if (useNullableContext)
        {
            writer.WriteLine("#nullable disable");
        }

        return new CSharpCodeWritingScope(writer);

        void WriteOptionalToken(IntermediateToken? token)
        {
            if (token is not null)
            {
                WriteToken(token);
            }
        }

        void WriteToken(IntermediateToken token)
        {
            if (token.Source is { } source)
            {
                WriteWithPragma(writer, token.Content, context, source);
            }
            else
            {
                writer.Write(token.Content);
            }
        }

        static void WriteWithPragma(CodeWriter writer, string content, CodeRenderingContext context, SourceSpan source)
        {
            if (context.Options.DesignTime)
            {
                using (writer.BuildLinePragma(source, context))
                {
                    context.AddSourceMappingFor(source);
                    writer.Write(content);
                }
            }
            else
            {
                using (writer.BuildEnhancedLinePragma(source, context))
                {
                    writer.Write(content);
                }
            }
        }
    }
}
