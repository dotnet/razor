// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using static Microsoft.AspNetCore.Razor.Language.CodeGeneration.CSharpStrings;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

internal static class CodeWriterExtensions
{
    private static readonly char[] CStyleStringLiteralEscapeChars =
    {
        '\r',
        '\t',
        '\"',
        '\'',
        '\\',
        '\0',
        '\n',
        '\u2028',
        '\u2029',
    };

    public static bool IsAtBeginningOfLine(this CodeWriter writer)
    {
        return writer.LastChar is '\n';
    }

    public static CodeWriter WritePadding(this CodeWriter writer, int offset, SourceSpan? span, CodeRenderingContext context)
    {
        if (span is not SourceSpan spanValue)
        {
            return writer;
        }

        if (context.SourceDocument.FilePath != null &&
            !string.Equals(context.SourceDocument.FilePath, spanValue.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            // We don't want to generate padding for nodes from imports.
            return writer;
        }

        var basePadding = CalculatePadding();
        var resolvedPadding = Math.Max(basePadding - offset, 0);

        writer.Indent(resolvedPadding);

        return writer;

        int CalculatePadding()
        {
            var spaceCount = 0;
            for (var i = span.Value.AbsoluteIndex - 1; i >= 0; i--)
            {
                var @char = context.SourceDocument.Text[i];
                if (@char == '\n' || @char == '\r')
                {
                    break;
                }
                else
                {
                    // Note that a tab is also replaced with a single space so character indices match.
                    spaceCount++;
                }
            }

            return spaceCount;
        }
    }

    public static CodeWriter WriteVariableDeclaration(this CodeWriter writer, string type, string name, string? value)
    {
        if (value.IsNullOrEmpty())
        {
            value = Null;
        }

        return writer.WriteLine($"{type}{Space}{name}{Assignment}{value}{Semicolon}");
    }

    public static CodeWriter WriteBooleanLiteral(this CodeWriter writer, bool value)
        => writer.Write(value ? True : False);

    public static CodeWriter WriteStartAssignment(this CodeWriter writer, string name)
        => writer.Write($"{name}{Assignment}");

    public static CodeWriter WriteParameterSeparator(this CodeWriter writer)
        => writer.Write(CommaSeparator);

    public static CodeWriter WriteStartNewObject(this CodeWriter writer, string typeName)
        => writer.Write($"new{Space}{typeName}{OpenParen}");

    public static CodeWriter WriteStringLiteral(this CodeWriter writer, string literal)
        => writer.WriteStringLiteral(literal.AsMemory());

    public static CodeWriter WriteStringLiteral(this CodeWriter writer, ReadOnlyMemory<char> literal)
    {
        if (literal.Length >= 256 && literal.Length <= 1500 && literal.Span.IndexOf('\0') == -1)
        {
            WriteVerbatimStringLiteral(writer, literal);
        }
        else
        {
            WriteCStyleStringLiteral(writer, literal);
        }

        return writer;
    }

    public static CodeWriter WriteUsing(this CodeWriter writer, string name)
    {
        return WriteUsing(writer, name, endLine: true);
    }

    public static CodeWriter WriteUsing(this CodeWriter writer, string name, bool endLine)
    {
        writer.Write($"using{Space}{name}");

        if (endLine)
        {
            writer.WriteLine(Semicolon);
        }

        return writer;
    }

    public static CodeWriter WriteEnhancedLineNumberDirective(this CodeWriter writer, SourceSpan span, int characterOffset, bool ensurePathBackslashes)
    {
        // All values here need to be offset by 1 since #line uses a 1-indexed numbering system.
        var lineNumberAsString = (span.LineIndex + 1).ToString(CultureInfo.InvariantCulture);
        var characterStartAsString = (span.CharacterIndex + 1).ToString(CultureInfo.InvariantCulture);
        var lineEndAsString = (span.LineIndex + 1 + span.LineCount).ToString(CultureInfo.InvariantCulture);
        var characterEndAsString = (span.EndCharacterIndex + 1).ToString(CultureInfo.InvariantCulture);

        writer.Write($"{HashLine}{Space}({lineNumberAsString},{characterStartAsString})-({lineEndAsString},{characterEndAsString}){Space}");

        // an offset of zero is indicated by its absence.
        if (characterOffset != 0)
        {
            var characterOffsetAsString = characterOffset.ToString(CultureInfo.InvariantCulture);
            writer.Write($"{characterOffsetAsString}{Space}");
        }

        return writer.Write(DoubleQuote).WriteFilePath(span.FilePath, ensurePathBackslashes).WriteLine(DoubleQuote);
    }

    public static CodeWriter WriteLineNumberDirective(this CodeWriter writer, SourceSpan span, bool ensurePathBackslashes)
    {
        if (writer.Length >= writer.NewLine.Length && !IsAtBeginningOfLine(writer))
        {
            writer.WriteLine();
        }

        var lineNumberAsString = (span.LineIndex + 1).ToString(CultureInfo.InvariantCulture);

        return writer
            .Write($"{HashLine}{Space}{lineNumberAsString}{Space}{DoubleQuote}")
            .WriteFilePath(span.FilePath, ensurePathBackslashes)
            .WriteLine(DoubleQuote);
    }

    private static CodeWriter WriteFilePath(this CodeWriter writer, string filePath, bool ensurePathBackslashes)
    {
        if (!ensurePathBackslashes)
        {
            return writer.Write(filePath);
        }

        // ISSUE: https://github.com/dotnet/razor/issues/9108
        // The razor tooling normalizes paths to be forward slash based, regardless of OS.
        // If you try and use the line pragma in the design time docs to map back to the original file it will fail,
        // as the path isn't actually valid on windows. As a workaround we apply a simple heuristic to switch the
        // paths back when writing out the design time paths.
        var filePathMemory = filePath.AsMemory();
        var forwardSlashIndex = filePathMemory.Span.IndexOf('/');
        while (forwardSlashIndex >= 0)
        {
            writer.Write(filePathMemory[..forwardSlashIndex]);
            writer.Write("\\");

            filePathMemory = filePathMemory[(forwardSlashIndex + 1)..];
            forwardSlashIndex = filePathMemory.Span.IndexOf('/');
        }

        writer.Write(filePathMemory);

        return writer;
    }

    public static CodeWriter WriteStartMethodInvocation(this CodeWriter writer, string methodName)
    {
        return writer.Write($"{methodName}{OpenParen}");
    }

    public static CodeWriter WriteEndMethodInvocation(this CodeWriter writer)
    {
        return WriteEndMethodInvocation(writer, endLine: true);
    }

    public static CodeWriter WriteEndMethodInvocation(this CodeWriter writer, bool endLine)
    {
        writer.Write(CloseParen);

        if (endLine)
        {
            writer.WriteLine(Semicolon);
        }

        return writer;
    }

    // Writes a method invocation for the given instance name.
    public static CodeWriter WriteInstanceMethodInvocation(
        this CodeWriter writer,
        string instanceName,
        string methodName,
        params ImmutableArray<string> arguments)
    {
        return WriteInstanceMethodInvocation(writer, instanceName, methodName, endLine: true, arguments);
    }

    // Writes a method invocation for the given instance name.
    public static CodeWriter WriteInstanceMethodInvocation(
        this CodeWriter writer,
        string instanceName,
        string methodName,
        bool endLine,
        params ImmutableArray<string> arguments)
    {
        return writer.WriteMethodInvocation($"{instanceName}.{methodName}", endLine, arguments);
    }

    public static CodeWriter WriteStartInstanceMethodInvocation(this CodeWriter writer, string instanceName, string methodName)
    {
        return writer.WriteStartMethodInvocation($"{instanceName}.{methodName}");
    }

    public static CodeWriter WriteField(
        this CodeWriter writer,
        ImmutableArray<string> suppressWarnings,
        ImmutableArray<string> modifiers,
        string typeName,
        string fieldName)
    {
        foreach (var supressWarnings in suppressWarnings)
        {
            writer.WriteLine($"#pragma warning disable {supressWarnings}");
        }

        if (modifiers.Length > 0)
        {
            writer.WriteSeparatedList(Space, modifiers);
            writer.Write(Space);
        }

        writer.WriteLine($"{typeName}{Space}{fieldName}{Semicolon}");

        for (var i = suppressWarnings.Length - 1; i >= 0; i--)
        {
            writer.WriteLine($"#pragma warning restore {suppressWarnings[i]}");
        }

        return writer;
    }

    public static CodeWriter WriteMethodInvocation(this CodeWriter writer, string methodName, params ImmutableArray<string> arguments)
    {
        return WriteMethodInvocation(writer, methodName, endLine: true, arguments);
    }

    public static CodeWriter WriteMethodInvocation(this CodeWriter writer, string methodName, bool endLine, params ImmutableArray<string> arguments)
    {
        return
            WriteStartMethodInvocation(writer, methodName)
            .WriteSeparatedList(CommaSeparator, arguments)
            .WriteEndMethodInvocation(endLine);
    }

    public static CodeWriter WriteSeparatedList(this CodeWriter writer, string separator, ImmutableArray<string> values)
    {
        if (values.Length > 0)
        {
            writer.Write(values[0]);
        }

        for (var i = 1; i < values.Length; i++)
        {
            writer.Write(separator);
            writer.Write(values[i]);
        }

        return writer;
    }

    public static CodeWriter WriteMemberExpressionBody(this CodeWriter writer, string expression)
        => writer.WriteLine($"{LambdaArrow}{expression}{Semicolon}");

    /// <summary>
    /// Writes an "@" character if the provided identifier needs escaping in c#
    /// </summary>
    public static CodeWriter WriteIdentifierEscapeIfNeeded(this CodeWriter writer, string identifier)
    {
        if (IdentifierRequiresEscaping(identifier))
        {
            writer.Write("@");
        }

        return writer;
    }

    public static bool IdentifierRequiresEscaping(this string identifier)
    {
        return CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(identifier) != CodeAnalysis.CSharp.SyntaxKind.None ||
            CodeAnalysis.CSharp.SyntaxFacts.GetContextualKeywordKind(identifier) != CodeAnalysis.CSharp.SyntaxKind.None;
    }

    public static CSharpCodeWritingScope BuildScope(this CodeWriter writer)
    {
        return new CSharpCodeWritingScope(writer);
    }

    public static CSharpCodeWritingScope BuildLambda(this CodeWriter writer, params ImmutableArray<string> parameterNames)
    {
        return BuildLambda(writer, async: false, parameterNames);
    }

    public static CSharpCodeWritingScope BuildAsyncLambda(this CodeWriter writer, params ImmutableArray<string> parameterNames)
    {
        return BuildLambda(writer, async: true, parameterNames);
    }

    private static CSharpCodeWritingScope BuildLambda(CodeWriter writer, bool async, ImmutableArray<string> parameterNames)
    {
        if (async)
        {
            writer.Write(Async);
        }

        writer
            .Write(OpenParen)
            .WriteSeparatedList(CommaSeparator, parameterNames)
            .Write(CloseParen)
            .Write(LambdaArrow);

        return new CSharpCodeWritingScope(writer);
    }

    public static CSharpCodeWritingScope BuildNamespace(this CodeWriter writer, string? name, SourceSpan? span, CodeRenderingContext context)
    {
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
        this CodeWriter writer,
        IList<string> modifiers,
        string name,
        BaseTypeWithModel? baseType,
        IList<IntermediateToken> interfaces,
        IList<TypeParameter> typeParameters,
        CodeRenderingContext context,
        bool useNullableContext = false)
    {
        Debug.Assert(context.CodeWriter == writer);

        if (useNullableContext)
        {
            writer.WriteLine("#nullable restore");
        }

        for (var i = 0; i < modifiers.Count; i++)
        {
            writer.Write(modifiers[i]);
            writer.Write(Space);
        }

        writer.Write("class ");
        writer.Write(name);

        if (typeParameters != null && typeParameters.Count > 0)
        {
            writer.Write("<");

            for (var i = 0; i < typeParameters.Count; i++)
            {
                var typeParameter = typeParameters[i];
                if (typeParameter.ParameterNameSource is { } source)
                {
                    WriteWithPragma(context, typeParameter.ParameterName, source);
                }
                else
                {
                    writer.Write(typeParameter.ParameterName);
                }

                // Write ',' between parameters, but not after them
                if (i < typeParameters.Count - 1)
                {
                    writer.Write(",");
                }
            }

            writer.Write(">");
        }

        var hasBaseType = !string.IsNullOrWhiteSpace(baseType?.BaseType.Content);
        var hasInterfaces = interfaces != null && interfaces.Count > 0;

        if (hasBaseType || hasInterfaces)
        {
            writer.Write(TypeListSeparator);

            if (hasBaseType)
            {
                Assumed.NotNull(baseType);

                WriteToken(baseType.BaseType);
                WriteOptionalToken(baseType.GreaterThan);
                WriteOptionalToken(baseType.ModelType);
                WriteOptionalToken(baseType.LessThan);

                if (hasInterfaces)
                {
                    writer.Write(CommaSeparator);
                }
            }

            if (hasInterfaces)
            {
                Assumed.NotNull(interfaces);

                WriteToken(interfaces[0]);

                for (var i = 1; i < interfaces.Count; i++)
                {
                    writer.Write(CommaSeparator);
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
            if (context.Options.DesignTime)
            {
                using (context.BuildLinePragma(source))
                {
                    context.AddSourceMappingFor(source);
                    context.CodeWriter.Write(content);
                }
            }
            else
            {
                using (context.BuildEnhancedLinePragma(source))
                {
                    context.CodeWriter.Write(content);
                }
            }
        }
    }

    public static CSharpCodeWritingScope BuildMethodDeclaration(
        this CodeWriter writer,
        string accessibility,
        string returnType,
        string name,
        params ImmutableArray<(string type, string name)> parameters)
    {
        writer.Write($"{accessibility}{Space}{returnType}{Space}{name}{OpenParen}");

        var first = true;

        foreach (var (paramType, paramName) in parameters)
        {
            if (!first)
            {
                writer.Write(CommaSeparator);
            }
            else
            {
                first = false;
            }

            writer.Write($"{paramType}{Space}{paramName}");
        }

        writer.WriteLine(OpenBrace);

        return new CSharpCodeWritingScope(writer);
    }

    private static void WriteVerbatimStringLiteral(CodeWriter writer, ReadOnlyMemory<char> literal)
    {
        writer.Write(VerbatimDoubleQuote);

        // We need to suppress indenting during the writing of the string's content. A
        // verbatim string literal could contain newlines that don't get escaped.
        var oldIndent = writer.CurrentIndent;
        writer.CurrentIndent = 0;

        // We need to find the index of each '"' (double-quote) to escape it.
        int index;
        while ((index = literal.Span.IndexOf('"')) >= 0)
        {
            writer.Write(literal[..index]);
            writer.Write(EmptyQuotes);

            literal = literal[(index + 1)..];
        }

        Debug.Assert(index == -1); // We've hit all of the double-quotes.

        // Write the remainder after the last double-quote.
        writer.Write(literal);

        writer.Write(DoubleQuote);

        writer.CurrentIndent = oldIndent;
    }

    private static void WriteCStyleStringLiteral(CodeWriter writer, ReadOnlyMemory<char> literal)
    {
        // From CSharpCodeGenerator.QuoteSnippetStringCStyle in CodeDOM
        writer.Write(DoubleQuote);

        // We need to find the index of each escapable character to escape it.
        int index;
        while ((index = literal.Span.IndexOfAny(CStyleStringLiteralEscapeChars)) >= 0)
        {
            writer.Write(literal[..index]);

            switch (literal.Span[index])
            {
                case '\r':
                    writer.Write("\\r");
                    break;
                case '\t':
                    writer.Write("\\t");
                    break;
                case '\"':
                    writer.Write("\\\"");
                    break;
                case '\'':
                    writer.Write("\\\'");
                    break;
                case '\\':
                    writer.Write("\\\\");
                    break;
                case '\0':
                    writer.Write("\\\0");
                    break;
                case '\n':
                    writer.Write("\\n");
                    break;
                case '\u2028':
                    writer.Write("\\u2028");
                    break;
                case '\u2029':
                    writer.Write("\\u2029");
                    break;
                default:
                    Debug.Assert(false, "Unknown escape character.");
                    break;
            }

            literal = literal[(index + 1)..];
        }

        Debug.Assert(index == -1); // We've hit all of chars that need escaping.

        // Write the remainder after the last escaped char.
        writer.Write(literal);

        writer.Write(DoubleQuote);
    }

    public struct CSharpCodeWritingScope : IDisposable
    {
        private readonly CodeWriter _writer;
        private readonly bool _autoSpace;
        private readonly bool _writeBraces;
        private readonly int _tabSize;
        private int _startIndent;

        public CSharpCodeWritingScope(CodeWriter writer, bool autoSpace = true, bool writeBraces = true)
        {
            _writer = writer;
            _autoSpace = autoSpace;
            _writeBraces = writeBraces;
            _tabSize = writer.TabSize;
            _startIndent = -1; // Set in WriteStartScope

            WriteStartScope();
        }

        public void Dispose()
        {
            WriteEndScope();
        }

        private void WriteStartScope()
        {
            TryAutoSpace(" ");

            if (_writeBraces)
            {
                _writer.WriteLine(OpenBrace);
            }
            else
            {
                _writer.WriteLine();
            }

            _writer.CurrentIndent += _tabSize;
            _startIndent = _writer.CurrentIndent;
        }

        private void WriteEndScope()
        {
            TryAutoSpace(_writer.NewLine);

            // Ensure the scope hasn't been modified
            if (_writer.CurrentIndent == _startIndent)
            {
                _writer.CurrentIndent -= _tabSize;
            }

            if (_writeBraces)
            {
                _writer.WriteLine(CloseBrace);
            }
            else
            {
                _writer.WriteLine();
            }
        }

        private void TryAutoSpace(string spaceCharacter)
        {
            if (_autoSpace &&
                _writer.LastChar is char ch &&
                !char.IsWhiteSpace(ch))
            {
                _writer.Write(spaceCharacter);
            }
        }
    }
}
