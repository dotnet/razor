﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using LspDiagnostic = Microsoft.VisualStudio.LanguageServer.Protocol.Diagnostic;
using LspDiagnosticSeverity = Microsoft.VisualStudio.LanguageServer.Protocol.DiagnosticSeverity;

namespace Microsoft.CodeAnalysis.Razor.Diagnostics;

internal static class TaskListDiagnosticProvider
{
    private static readonly DiagnosticTag[] s_taskItemTags = [VSDiagnosticTags.TaskItem];

    public static ImmutableArray<LspDiagnostic> GetTaskListDiagnostics(RazorCodeDocument codeDocument, ImmutableArray<string> taskListDescriptors)
    {
        var source = codeDocument.Source.Text;
        var tree = codeDocument.GetSyntaxTree();

        using var diagnostics = new PooledArrayBuilder<LspDiagnostic>();

        foreach (var node in tree.Root.DescendantNodes())
        {
            if (node is RazorCommentBlockSyntax comment)
            {
                var i = comment.Comment.SpanStart;

                while (char.IsWhiteSpace(source[i]))
                {
                    i++;
                }

                foreach (var token in taskListDescriptors)
                {
                    if (!CommentMatchesToken(source, comment, i, token))
                    {
                        continue;
                    }

                    diagnostics.Add(new LspDiagnostic
                    {
                        Code = "TODO",
                        Message = comment.Comment.Content.Trim(),
                        Source = "Razor",
                        Severity = LspDiagnosticSeverity.Information,
                        Range = source.GetRange(comment.Comment.Span),
                        Tags = s_taskItemTags
                    });

                    break;
                }
            }
        }

        return diagnostics.ToImmutable();
    }

    private static bool CommentMatchesToken(SourceText source, RazorCommentBlockSyntax comment, int i, string token)
    {
        if (i + token.Length + 2 > comment.EndCommentStar.SpanStart)
        {
            // Not enough room in the comment for the token and some content
            return false;
        }

        for (var j = 0; j < token.Length; j++)
        {
            if (source.Length < i + j)
            {
                return false;
            }

            if (char.ToLowerInvariant(source[i + j]) != char.ToLowerInvariant(token[j]))
            {
                return false;
            }
        }

        if (char.IsLetter(source[i + token.Length + 1]))
        {
            // The comment starts with the token, but the next character is a letter, which means it is something like "TODONT"
            return false;
        }

        return true;
    }
}
