// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
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

        using var documentChanges = new PooledArrayBuilder<TextDocumentEdit>();
        var codeDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier() { DocumentUri = new(documentContext.Uri) };

        // Check if the @using directive already exists
        var hasUsing = HasUsingDirective(codeDocument, actionParams.Namespace);

        // If the using doesn't exist, add it
        if (!hasUsing)
        {
            var usingEdit = AddUsingsHelper.CreateAddUsingTextEdit(actionParams.Namespace, codeDocument);
            documentChanges.Add(new TextDocumentEdit()
            {
                TextDocument = codeDocumentIdentifier,
                Edits = [usingEdit]
            });
        }

        // Find all instances of the fully qualified component tag and replace them
        var syntaxTree = codeDocument.GetSyntaxTree();
        if (syntaxTree?.Root is not null)
        {
            using var textEdits = new PooledArrayBuilder<SumType<TextEdit, AnnotatedTextEdit>>();

            foreach (var node in syntaxTree.Root.DescendantNodes())
            {
                if (node is MarkupTagHelperElementSyntax tagHelperElement &&
                    tagHelperElement.StartTag is not null)
                {
                    var tagName = tagHelperElement.StartTag.Name.Content;
                    if (tagName == actionParams.FullyQualifiedName)
                    {
                        // Calculate the simple name (everything after the last dot)
                        var lastDotIndex = tagName.LastIndexOf('.');
                        var simpleName = tagName[(lastDotIndex + 1)..];

                        // Replace start tag
                        var startTagRange = tagHelperElement.StartTag.Name.GetRange(codeDocument.Source);
                        textEdits.Add(LspFactory.CreateTextEdit(startTagRange, simpleName));

                        // Replace end tag if it exists
                        if (tagHelperElement.EndTag is not null)
                        {
                            var endTagRange = tagHelperElement.EndTag.Name.GetRange(codeDocument.Source);
                            textEdits.Add(LspFactory.CreateTextEdit(endTagRange, simpleName));
                        }
                    }
                }
            }

            if (textEdits.Count > 0)
            {
                documentChanges.Add(new TextDocumentEdit()
                {
                    TextDocument = codeDocumentIdentifier,
                    Edits = textEdits.ToArray()
                });
            }
        }

        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges.ToArray(),
        };
    }

    private static bool HasUsingDirective(Microsoft.AspNetCore.Razor.Language.RazorCodeDocument codeDocument, string @namespace)
    {
        var syntaxTree = codeDocument.GetSyntaxTree();
        if (syntaxTree?.Root is null)
        {
            return false;
        }

        foreach (var node in syntaxTree.Root.DescendantNodes())
        {
            if (node is RazorDirectiveSyntax directiveNode)
            {
                foreach (var child in directiveNode.DescendantNodes())
                {
                    if (child.GetChunkGenerator() is AddImportChunkGenerator { IsStatic: false } usingStatement)
                    {
                        if (usingStatement.ParsedNamespace == @namespace)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
