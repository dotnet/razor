// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using LspDiagnostic = Microsoft.VisualStudio.LanguageServer.Protocol.Diagnostic;
using LspDiagnosticSeverity = Microsoft.VisualStudio.LanguageServer.Protocol.DiagnosticSeverity;
using LspRange = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.CodeAnalysis.Razor.Diagnostics;

internal static class TaskListDiagnosticProvider
{
    private static readonly DiagnosticTag[] s_taskItemTags = [VSDiagnosticTags.TaskItem];

    public static VSInternalDiagnosticReport[] GetTaskListDiagnostics(RazorCodeDocument codeDocument, ImmutableArray<string> taskListDescriptors)
    {
        var source = codeDocument.Source.Text;
        var tree = codeDocument.GetSyntaxTree();

        using var _ = ListPool<LspDiagnostic>.GetPooledObject(out var diagnostics);

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
                    if (i + token.Length + 2 > comment.EndCommentStar.SpanStart || // Enough room in the comment for the token and some content?
                        !Matches(source, i, token) ||                              // Does the prefix match?
                        char.IsLetter(source[i + token.Length + 1]))               // Is there something after the prefix, so we don't match "TODOLOL"
                    {
                        continue;
                    }

                    AddTaskDiagnostic(diagnostics, source.GetRange(comment.Comment.Span), comment.Comment.Content.Trim());
                    break;
                }
            }
        }

        return
        [
            new VSInternalDiagnosticReport
            {
                ResultId = Guid.NewGuid().ToString(),
                Diagnostics = [.. diagnostics]
            }
        ];
    }

    private static bool Matches(SourceText source, int i, string token)
    {
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

        return true;
    }

    private static void AddTaskDiagnostic(List<LspDiagnostic> diagnostics, LspRange range, string message)
    {
        diagnostics.Add(new LspDiagnostic
        {
            Code = "TODO",
            Message = message,
            Source = "Razor",
            Severity = LspDiagnosticSeverity.Information,
            Range = range,
            Tags = s_taskItemTags
        });
    }
}
