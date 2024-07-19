// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal sealed class CloseTextTagOnAutoInsertProvider(ILoggerFactory loggerFactory) : IOnAutoInsertProvider
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<IOnAutoInsertProvider>();

    public string TriggerCharacter => ">";

    public async ValueTask<InsertTextEdit?> TryResolveInsertionAsync(Position position, IDocumentSnapshot documentSnapshot, bool autoClosingTags)
    {
        if (!(autoClosingTags
              && await IsAtTextTagAsync(documentSnapshot, position, _logger).ConfigureAwait(false)))
        {
            return default;
        }

        // This is a text tag.
        var format = InsertTextFormat.Snippet;
        var edit = new TextEdit()
        {
            NewText = $"$0</{SyntaxConstants.TextTagName}>",
            Range = new Range { Start = position, End = position },
        };

        return new InsertTextEdit(edit, format);
    }

    private static async ValueTask<bool> IsAtTextTagAsync(IDocumentSnapshot documentSnapshot, Position position, ILogger logger)
    {
        var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
        var syntaxTree = codeDocument.GetSyntaxTree();

        if (!(documentSnapshot.TryGetText(out var sourceText)
              && position.TryGetAbsoluteIndex(sourceText, logger, out var absoluteIndex)))
        {
            return false;
        }

        var owner = syntaxTree.Root.FindToken(absoluteIndex - 1);
        // Make sure the end </text> tag doesn't already exist
        if (owner?.Parent is MarkupStartTagSyntax
            {
                IsMarkupTransition: true,
                Parent: MarkupElementSyntax { EndTag: null }
            } startTag)
        {
            Debug.Assert(string.Equals(startTag.Name.Content, SyntaxConstants.TextTagName, StringComparison.Ordinal), "MarkupTransition that is not a <text> tag.");

            return true;
        }

        return false;
    }
}
