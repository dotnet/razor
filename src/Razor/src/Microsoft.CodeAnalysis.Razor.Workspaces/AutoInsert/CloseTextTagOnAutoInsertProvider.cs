// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal class CloseTextTagOnAutoInsertProvider : IOnAutoInsertProvider
{
    public string TriggerCharacter => ">";

    public async ValueTask<InsertTextEdit?> TryResolveInsertionAsync(Position position, IDocumentSnapshot documentSnapshot, bool enableAutoClosingTags)
    {
        if (!(enableAutoClosingTags
              && await IsAtTextTagAsync(documentSnapshot, position).ConfigureAwait(false)))
        {
            return default;
        }

        // This is a text tag.
        var format = InsertTextFormat.Snippet;
        var edit = VsLspFactory.CreateTextEdit(position, $"$0</{SyntaxConstants.TextTagName}>");

        return new InsertTextEdit(edit, format);
    }

    private static async ValueTask<bool> IsAtTextTagAsync(IDocumentSnapshot documentSnapshot, Position position)
    {
        var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
        var syntaxTree = codeDocument.GetSyntaxTree();

        if (!(documentSnapshot.TryGetText(out var sourceText)
              && sourceText.TryGetAbsoluteIndex(position, out var absoluteIndex)))
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
