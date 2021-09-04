﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class RazorSyntaxTreeExtensions
    {
        public static IReadOnlyList<FormattingSpan> GetFormattingSpans(this RazorSyntaxTree syntaxTree)
        {
            if (syntaxTree == null)
            {
                throw new ArgumentNullException(nameof(syntaxTree));
            }

            var visitor = new FormattingVisitor(syntaxTree.Source);
            visitor.Visit(syntaxTree.Root);

            return visitor.FormattingSpans;
        }

        public static IReadOnlyList<RazorDirectiveSyntax> GetCodeBlockDirectives(this RazorSyntaxTree syntaxTree)
        {
            if (syntaxTree == null)
            {
                throw new ArgumentNullException(nameof(syntaxTree));
            }

            // We want all nodes of type RazorDirectiveSyntax which will contain code.
            // Since code block directives occur at the top-level, we don't need to dive deeper into unrelated nodes.
            var codeBlockDirectives = syntaxTree.Root
                .DescendantNodes(node => node is RazorDocumentSyntax || node is MarkupBlockSyntax || node is CSharpCodeBlockSyntax)
                .OfType<RazorDirectiveSyntax>()
                .Where(directive => directive.DirectiveDescriptor?.Kind == DirectiveKind.CodeBlock)
                .ToList();

            return codeBlockDirectives;
        }

        public static IReadOnlyList<CSharpStatementSyntax> GetCSharpStatements(this RazorSyntaxTree syntaxTree)
        {
            if (syntaxTree is null)
            {
                throw new ArgumentNullException(nameof(syntaxTree));
            }

            // We want all nodes that represent Razor C# statements, @{ ... }.
            var statements = syntaxTree.Root.DescendantNodes().OfType<CSharpStatementSyntax>().ToList();
            return statements;
        }

        public static SyntaxNode GetOwner(this RazorSyntaxTree syntaxTree, SourceText sourceText, Position position)
        {
            if (syntaxTree is null)
            {
                throw new ArgumentNullException(nameof(syntaxTree));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            if (position is null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            var absoluteIndex = position.GetAbsoluteIndex(sourceText);
            var change = new SourceChange(absoluteIndex, 0, string.Empty);
            var owner = syntaxTree.Root.LocateOwner(change);
            return owner;
        }

        public static SyntaxNode GetOwner(this RazorSyntaxTree syntaxTree, SourceText sourceText, Range range)
        {
            if (syntaxTree is null)
            {
                throw new ArgumentNullException(nameof(syntaxTree));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            if (range is null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            var absoluteStartIndex = range.Start.GetAbsoluteIndex(sourceText);
            var absoluteEndIndex = range.End.GetAbsoluteIndex(sourceText);
            var length = absoluteEndIndex - absoluteStartIndex;
            var change = new SourceChange(absoluteStartIndex, length, string.Empty);
            var owner = syntaxTree.Root.LocateOwner(change);
            return owner;
        }
    }
}
