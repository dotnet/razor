// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions;

internal class RemoveUnnecessaryDirectivesCodeActionResolver : IRazorCodeActionResolver
{
    public string Action => LanguageServerConstants.CodeActions.RemoveUnnecessaryDirectives;

    public async Task<WorkspaceEdit?> ResolveAsync(DocumentContext documentContext, JsonElement data, RazorFormattingOptions options, CancellationToken cancellationToken)
    {
        var actionParams = data.Deserialize<RemoveUnnecessaryDirectivesCodeActionParams>();
        if (actionParams is null)
        {
            return null;
        }

        var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);

        var edits = new SumType<TextEdit, AnnotatedTextEdit>[actionParams.UnusedDirectiveLines.Length];
        for (var i = 0; i < actionParams.UnusedDirectiveLines.Length; i++)
        {
            var line = sourceText.Lines[actionParams.UnusedDirectiveLines[i]];
            var removeRange = sourceText.GetRange(line.Start, line.EndIncludingLineBreak);
            edits[i] = LspFactory.CreateTextEdit(removeRange, string.Empty);
        }

        return new WorkspaceEdit
        {
            DocumentChanges = new SumType<TextDocumentEdit[], SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]>(
            [
                new TextDocumentEdit
                {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier() { DocumentUri = new(documentContext.Uri) },
                    Edits = edits,
                }
            ])
        };
    }
}
