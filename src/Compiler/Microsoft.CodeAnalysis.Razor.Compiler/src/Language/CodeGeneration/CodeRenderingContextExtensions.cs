// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Utilities;
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
            using (context.BuildEnhancedLinePragma(span))
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
                    WriteWithPragma(context, typeParameter.ParameterName, source);
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
                        WriteWithPragma(context, constraint, source);
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
                WriteWithPragma(context, token.Content, source);
            }
            else
            {
                writer.Write(token.Content);
            }
        }

        static void WriteWithPragma(CodeRenderingContext context, string content, SourceSpan source)
        {
            var writer = context.CodeWriter;

            if (context.Options.DesignTime)
            {
                using (context.BuildLinePragma(source))
                {
                    context.AddSourceMappingFor(source);
                    writer.Write(content);
                }
            }
            else
            {
                using (context.BuildEnhancedLinePragma(source))
                {
                    writer.Write(content);
                }
            }
        }
    }

    public static void WritePropertyDeclaration(
        this CodeRenderingContext context,
        ImmutableArray<string> modifiers,
        IntermediateToken type,
        string propertyName,
        string propertyExpression)
    {
        context.WritePropertyDeclarationPreamble(modifiers, type.Content, propertyName, type.Source, propertySpan: null);

        var writer = context.CodeWriter;
        writer.Write(" => ");
        writer.Write(propertyExpression);
        writer.WriteLine(";");
    }

    public static void WriteAutoPropertyDeclaration(
        this CodeRenderingContext context,
        ImmutableArray<string> modifiers,
        string typeName,
        string propertyName,
        SourceSpan? typeSpan = null,
        SourceSpan? propertySpan = null,
        bool privateSetter = false,
        bool defaultValue = false)
    {
        context.WritePropertyDeclarationPreamble(modifiers, typeName, propertyName, typeSpan, propertySpan);

        var writer = context.CodeWriter;

        writer.Write(" { get;");

        if (privateSetter)
        {
            writer.Write(" private");
        }

        writer.Write(" set; }");
        writer.WriteLine();

        if (defaultValue && context?.Options.SuppressNullabilityEnforcement == false && context?.Options.DesignTime == false)
        {
            writer.WriteLine(" = default!;");
        }
    }

    private static void WritePropertyDeclarationPreamble(
        this CodeRenderingContext context,
        ImmutableArray<string> modifiers,
        string typeName,
        string propertyName,
        SourceSpan? typeSpan,
        SourceSpan? propertySpan)
    {
        var writer = context.CodeWriter;

        foreach (var modifier in modifiers)
        {
            writer.Write(modifier);
            writer.Write(" ");
        }

        WriteToken(context, typeName, typeSpan);
        writer.Write(" ");
        WriteToken(context, propertyName, propertySpan);

        static void WriteToken(CodeRenderingContext context, string content, SourceSpan? span)
        {
            var writer = context.CodeWriter;

            if (span is not null && context.Options.DesignTime == false)
            {
                using (context.BuildEnhancedLinePragma(span))
                {
                    writer.Write(content);
                }
            }
            else
            {
                writer.Write(content);
            }
        }
    }

    public static LinePragmaScope BuildLinePragma(
        this CodeRenderingContext context,
        SourceSpan? span,
        bool suppressLineDefaultAndHidden = false)
    {
        if (string.IsNullOrEmpty(span?.FilePath))
        {
            // Can't build a valid line pragma without a file path.
            return default;
        }

        return new LinePragmaScope(context, span.Value, 0, useEnhancedLinePragma: false, suppressLineDefaultAndHidden);
    }

    public static LinePragmaScope BuildEnhancedLinePragma(
        this CodeRenderingContext context,
        SourceSpan? span,
        int characterOffset = 0,
        bool suppressLineDefaultAndHidden = false)
    {
        if (string.IsNullOrEmpty(span?.FilePath))
        {
            // Can't build a valid line pragma without a file path.
            return default;
        }

        return new LinePragmaScope(context, span.Value, characterOffset, useEnhancedLinePragma: true, suppressLineDefaultAndHidden);
    }

    public readonly ref struct LinePragmaScope
    {
        private readonly CodeRenderingContext _context;
        private readonly int _startIndent;
        private readonly int _startLineIndex;
        private readonly SourceSpan _span;
        private readonly bool _suppressLineDefaultAndHidden;

        public LinePragmaScope(
            CodeRenderingContext context,
            SourceSpan span,
            int characterOffset,
            bool useEnhancedLinePragma = false,
            bool suppressLineDefaultAndHidden = false)
        {
            Debug.Assert(context.Options.DesignTime || useEnhancedLinePragma, "Runtime generation should only use enhanced line pragmas");

            _context = context;
            _suppressLineDefaultAndHidden = suppressLineDefaultAndHidden;
            _span = span;

            var writer = context.CodeWriter;
            _startIndent = writer.CurrentIndent;
            writer.CurrentIndent = 0;

            var endsWithNewline = writer.LastChar is '\n';
            if (!endsWithNewline)
            {
                writer.WriteLine();
            }

            if (!_context.Options.SuppressNullabilityEnforcement)
            {
                writer.WriteLine("#nullable restore");
            }

            var ensurePathBackslashes = context.Options.RemapLinePragmaPathsOnWindows && PlatformInformation.IsWindows;
            if (useEnhancedLinePragma && _context.Options.UseEnhancedLinePragma)
            {
                writer.WriteEnhancedLineNumberDirective(span, characterOffset, ensurePathBackslashes);
            }
            else
            {
                writer.WriteLineNumberDirective(span, ensurePathBackslashes);
            }

            // Capture the line index after writing the #line directive.
            _startLineIndex = writer.Location.LineIndex;

            if (useEnhancedLinePragma)
            {
                // If the caller requested an enhanced line directive, but we fell back to regular ones, write out the extra padding that is required
                if (!_context.Options.UseEnhancedLinePragma)
                {
                    writer.WritePadding(0, span, context);
                    characterOffset = 0;
                }

                context.AddSourceMappingFor(span, characterOffset);
            }
        }

        public void Dispose()
        {
            if (_context is null)
            {
                return;
            }

            var writer = _context.CodeWriter;

            // Need to add an additional line at the end IF there wasn't one already written.
            // This is needed to work with the C# editor's handling of #line ...
            var endsWithNewline = writer.LastChar is '\n';

            // Always write at least 1 empty line to potentially separate code from pragmas.
            writer.WriteLine();

            // Check if the previous empty line wasn't enough to separate code from pragmas.
            if (!endsWithNewline)
            {
                writer.WriteLine();
            }

            var lineCount = writer.Location.LineIndex - _startLineIndex;
            var linePragma = new LinePragma(_span.LineIndex, lineCount, _span.FilePath, _span.EndCharacterIndex, _span.EndCharacterIndex, _span.CharacterIndex);
            _context.AddLinePragma(linePragma);

            if (!_suppressLineDefaultAndHidden)
            {
                writer
                    .WriteLine("#line default")
                    .WriteLine("#line hidden");
            }

            if (!_context.Options.SuppressNullabilityEnforcement)
            {
                writer.WriteLine("#nullable disable");
            }

            writer.CurrentIndent = _startIndent;
        }
    }
}
