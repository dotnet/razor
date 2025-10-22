// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

internal class SimplifyFullyQualifiedComponentCodeActionResolver : IRazorCodeActionResolver
{
    public string Action => LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent;

    public async Task<WorkspaceEdit?> ResolveAsync(DocumentContext documentContext, JsonElement data, RazorFormattingOptions options, CancellationToken cancellationToken)
    {
        if (data.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var actionParams = JsonSerializer.Deserialize<SimplifyFullyQualifiedComponentCodeActionParams>(data.GetRawText());
        if (actionParams is null)
        {
            return null;
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var text = codeDocument.Source.Text;

        var codeDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier() { DocumentUri = new(documentContext.Uri) };

        using var documentChanges = new PooledArrayBuilder<TextDocumentEdit>();

        // Check if the using directive already exists
        var syntaxTree = codeDocument.GetSyntaxTree();
        if (syntaxTree is null)
        {
            return null;
        }

        var existingUsings = syntaxTree.GetUsingDirectives();
        var namespaceAlreadyExists = existingUsings.Any(u =>
        {
            foreach (var child in u.DescendantNodes())
            {
                if (child.GetChunkGenerator() is AddImportChunkGenerator { IsStatic: false } usingStatement)
                {
                    return usingStatement.ParsedNamespace == actionParams.Namespace;
                }
            }

            return false;
        });

        // First, add the tag simplification edits (at the original positions in the document)
        using var tagEdits = new PooledArrayBuilder<TextEdit>();

        // Replace the fully qualified name with the simple component name in start tag
        var startTagRange = text.GetRange(actionParams.StartTagSpanStart, actionParams.StartTagSpanEnd);
        tagEdits.Add(new TextEdit
        {
            NewText = actionParams.ComponentName,
            Range = startTagRange,
        });

        // Replace the fully qualified name with the simple component name in end tag (if it exists)
        if (actionParams.EndTagSpanStart >= 0 && actionParams.EndTagSpanEnd >= 0)
        {
            var endTagRange = text.GetRange(actionParams.EndTagSpanStart, actionParams.EndTagSpanEnd);
            tagEdits.Add(new TextEdit
            {
                NewText = actionParams.ComponentName,
                Range = endTagRange,
            });
        }

        documentChanges.Add(new TextDocumentEdit()
        {
            TextDocument = codeDocumentIdentifier,
            Edits = tagEdits.ToArray().Select(e => (SumType<TextEdit, AnnotatedTextEdit>)e).ToArray()
        });

        // Then, add using directive if it doesn't already exist (at the top of the file)
        // This must come after the tag edits because the using directive will be inserted at the top,
        // which would change line numbers for subsequent edits
        if (!namespaceAlreadyExists)
        {
            var addUsingEdit = AddUsingsHelper.CreateAddUsingTextEdit(actionParams.Namespace, codeDocument);
            documentChanges.Add(new TextDocumentEdit()
            {
                TextDocument = codeDocumentIdentifier,
                Edits = [addUsingEdit]
            });
        }

        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges.ToArray(),
        };
    }
}
