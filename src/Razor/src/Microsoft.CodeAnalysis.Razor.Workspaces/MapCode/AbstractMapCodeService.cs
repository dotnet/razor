// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using RazorSyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.CodeAnalysis.Razor.MapCode;

internal abstract class AbstractMapCodeService(IDocumentMappingService documentMappingService) : IMapCodeService
{
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;

    protected abstract bool TryCreateDocumentContext(ISolutionQueryOperations queryOperations, Uri uri, [NotNullWhen(true)] out DocumentContext? documentContext);

    protected abstract Task<(Uri MappedDocumentUri, LinePositionSpan MappedRange)> MapToHostDocumentUriAndRangeAsync(DocumentContext documentContext, Uri generatedDocumentUri, LinePositionSpan generatedDocumentRange, CancellationToken cancellationToken);

    protected abstract Task<WorkspaceEdit?> GetCSharpMapCodeEditAsync(DocumentContext documentContext, Guid mapCodeCorrelationId, string nodeToMapContents, LspLocation[][] focusLocations, CancellationToken cancellationToken);

    public async Task<WorkspaceEdit?> MapCodeAsync(ISolutionQueryOperations queryOperations, VSInternalMapCodeMapping[] mappings, Guid mapCodeCorrelationId, CancellationToken cancellationToken)
    {
        using var _ = ListPool<TextDocumentEdit>.GetPooledObject(out var changes);
        foreach (var mapping in mappings)
        {
            if (mapping.TextDocument is null || mapping.FocusLocations is null)
            {
                continue;
            }

            foreach (var content in mapping.Contents)
            {
                var csharpFocusLocationsAndNodes = await GetCSharpFocusLocationsAndNodesAsync(queryOperations, mapping.TextDocument, mapping.FocusLocations, content, cancellationToken).ConfigureAwait(false);

                var csharpEdits = csharpFocusLocationsAndNodes is not null
                    ? await GetCSharpMapCodeEditsAsync(queryOperations, mapping.TextDocument, csharpFocusLocationsAndNodes, mapCodeCorrelationId, cancellationToken).ConfigureAwait(false)
                    : [];

                await MapCSharpEditsAndRazorCodeAsync(queryOperations, content, changes, csharpEdits, mapping.TextDocument, mapping.FocusLocations, cancellationToken).ConfigureAwait(false);
            }
        }

        if (changes.Count == 0)
        {
            // No changes were made, return null to indicate no edits.
            return null;
        }

        MergeEdits(changes);

        return new WorkspaceEdit
        {
            DocumentChanges = changes.ToArray()
        };
    }

    private async Task MapCSharpEditsAndRazorCodeAsync(ISolutionQueryOperations queryOperations, string content, List<TextDocumentEdit> changes, ImmutableArray<WorkspaceEdit> csharpEdits, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, CancellationToken cancellationToken)
    {
        if (!TryCreateDocumentContext(queryOperations, textDocument.DocumentUri.GetRequiredParsedUri(), out var documentContext))
        {
            return;
        }

        foreach (var edit in csharpEdits)
        {
            var csharpMappingSuccessful = await TryProcessCSharpEditAsync(documentContext, edit, changes, cancellationToken).ConfigureAwait(false);
            if (!csharpMappingSuccessful)
            {
                return;
            }
        }

        await TryMapRazorNodesAsync(documentContext, content, focusLocations, changes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CSharpFocusLocationsAndNodes?> GetCSharpFocusLocationsAndNodesAsync(ISolutionQueryOperations queryOperations, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, string? content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return null;
        }

        if (!TryCreateDocumentContext(queryOperations, textDocument.DocumentUri.GetRequiredParsedUri(), out var documentContext))
        {
            return null;
        }

        var nodesToMap = await GetNodesToMapAsync(documentContext, content, cancellationToken).ConfigureAwait(false);

        return await GetCSharpFocusLocationsAndNodesAsync(queryOperations, nodesToMap, focusLocations, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ImmutableArray<RazorSyntaxNode>> GetNodesToMapAsync(DocumentContext documentContext, string content, CancellationToken cancellationToken)
    {
        // We create a new Razor file based on each content in each mapping order to get the syntax tree that we'll later use to map.
        var newSnapshot = documentContext.Snapshot.WithText(SourceText.From(content));
        var codeToMap = await newSnapshot.GetGeneratedOutputAsync(cancellationToken).ConfigureAwait(false);
        var syntaxTree = codeToMap.GetSyntaxTree();
        if (syntaxTree is null)
        {
            return [];
        }

        var nodesToMap = ExtractValidNodesToMap(syntaxTree.Root);

        return nodesToMap;
    }

    private async Task<ImmutableArray<WorkspaceEdit>> GetCSharpMapCodeEditsAsync(ISolutionQueryOperations queryOperations, TextDocumentIdentifier textDocument, CSharpFocusLocationsAndNodes csharpFocusLocationsAndNodes, Guid mapCodeCorrelationId, CancellationToken cancellationToken)
    {
        if (!TryCreateDocumentContext(queryOperations, textDocument.DocumentUri.GetRequiredParsedUri(), out var documentContext))
        {
            return [];
        }

        using var csharpEdits = new PooledArrayBuilder<WorkspaceEdit>();

        foreach (var csharpBody in csharpFocusLocationsAndNodes.CSharpNodeBodies)
        {
            var csharpEdit = await GetCSharpMapCodeEditAsync(documentContext, mapCodeCorrelationId, csharpBody, csharpFocusLocationsAndNodes.FocusLocations, cancellationToken).ConfigureAwait(false);
            if (csharpEdit is null)
            {
                // It's likely an error occurred during C# mapping.
                return [];
            }

            csharpEdits.Add(csharpEdit);
        }

        return csharpEdits.ToImmutable();
    }

    private async Task<CSharpFocusLocationsAndNodes?> GetCSharpFocusLocationsAndNodesAsync(ISolutionQueryOperations queryOperations, ImmutableArray<RazorSyntaxNode> nodesToMap, LspLocation[][] locations, CancellationToken cancellationToken)
    {
        using var csharpNodes = new PooledArrayBuilder<string>();
        foreach (var nodeToMap in nodesToMap)
        {
            if (nodeToMap.IsCSharpNode(out var csharpBody))
            {
                csharpNodes.Add(csharpBody.ToString());
            }
        }

        if (csharpNodes.Count > 0)
        {
            var csharpFocusLocations = await GetCSharpFocusLocationsAsync(queryOperations, locations, cancellationToken).ConfigureAwait(false);

            return new CSharpFocusLocationsAndNodes(csharpFocusLocations, csharpNodes.ToArray());
        }

        return null;
    }

    private static async Task TryMapRazorNodesAsync(DocumentContext documentContext, string content, LspLocation[][] focusLocations, List<TextDocumentEdit> changes, CancellationToken cancellationToken)
    {
        var nodesToMap = await GetNodesToMapAsync(documentContext, content, cancellationToken).ConfigureAwait(false);
        if (nodesToMap.IsDefaultOrEmpty)
        {
            // No nodes to map or syntax root is null, nothing to do.
            return;
        }

        var syntaxTree = await documentContext.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var syntaxRoot = syntaxTree.Root;

        foreach (var locationByPriority in focusLocations)
        {
            foreach (var location in locationByPriority)
            {
                Debug.Assert(location.DocumentUri.GetRequiredParsedUri() == documentContext.Uri);

                using var razorNodesToMap = new PooledArrayBuilder<RazorSyntaxNode>();
                foreach (var nodeToMap in nodesToMap)
                {
                    // Only process new, non-C#, nodes
                    if (nodeToMap.IsCSharpNode(out _) || nodeToMap.ExistsOnTarget(syntaxRoot))
                    {
                        continue;
                    }

                    razorNodesToMap.Add(nodeToMap);
                }

                var sourceText = await documentContext.Snapshot.GetTextAsync(cancellationToken).ConfigureAwait(false);

                var mappingSuccess = false;
                foreach (var nodeToMap in razorNodesToMap)
                {
                    var insertionSpan = InsertMapper.GetInsertionPoint(syntaxRoot, sourceText, location);
                    if (insertionSpan is not null)
                    {
                        var textSpan = new TextSpan(insertionSpan.Value, 0);
                        var edit = LspFactory.CreateTextEdit(sourceText.GetRange(textSpan), nodeToMap.ToString());

                        var textDocumentEdit = new TextDocumentEdit
                        {
                            TextDocument = new OptionalVersionedTextDocumentIdentifier
                            {
                                DocumentUri = new(documentContext.Uri)
                            },
                            Edits = [edit],
                        };

                        changes.Add(textDocumentEdit);
                        mappingSuccess = true;
                    }
                }

                if (mappingSuccess)
                {
                    return;
                }
            }
        }
    }

    private static ImmutableArray<RazorSyntaxNode> ExtractValidNodesToMap(RazorSyntaxNode rootNode)
    {
        using var validNodesToMap = new PooledArrayBuilder<RazorSyntaxNode>();
        using var _ = StackPool<RazorSyntaxNode>.GetPooledObject(out var stack);
        stack.Push(rootNode);

        while (stack.Count > 0)
        {
            var currentNode = stack.Pop();

            if (s_validNodesToMap.Contains(currentNode.GetType()))
            {
                validNodesToMap.Add(currentNode);
                continue;
            }

            // Add child nodes to the stack in reverse order for depth-first search
            foreach (var childNode in currentNode.ChildNodes().Reverse())
            {
                stack.Push(childNode);
            }
        }

        return validNodesToMap.ToImmutable();
    }

    // These are the nodes that we currently support for mapping. We should update
    // this list as the client evolves to send more types of nodes.
    private readonly static List<Type> s_validNodesToMap =
    [
        typeof(CSharpCodeBlockSyntax),
        typeof(CSharpExplicitExpressionSyntax),
        typeof(CSharpImplicitExpressionSyntax),
        typeof(MarkupElementSyntax),
        typeof(MarkupTextLiteralSyntax),
        typeof(RazorDirectiveSyntax),
    ];

    private async Task<LspLocation[][]> GetCSharpFocusLocationsAsync(ISolutionQueryOperations queryOperations, LspLocation[][] focusLocations, CancellationToken cancellationToken)
    {
        // If the focus locations are in a C# context, map them to the C# document.
        var csharpFocusLocations = new LspLocation[focusLocations.Length][];
        using var csharpLocations = new PooledArrayBuilder<LspLocation>();
        for (var i = 0; i < focusLocations.Length; i++)
        {
            csharpLocations.Clear();

            var locations = focusLocations[i];
            foreach (var potentialLocation in locations)
            {
                if (potentialLocation is null)
                {
                    continue;
                }

                if (!TryCreateDocumentContext(queryOperations, potentialLocation.DocumentUri.GetRequiredParsedUri(), out var documentContext))
                {
                    continue;
                }

                var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
                var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
                var hostDocumentRange = potentialLocation.Range.ToLinePositionSpan();
                var csharpDocument = codeDocument.GetRequiredCSharpDocument();

                if (_documentMappingService.TryMapToCSharpDocumentRange(csharpDocument, hostDocumentRange, out var generatedDocumentRange))
                {
                    var csharpLocation = new LspLocation
                    {
                        // We convert the URI to the C# generated document URI later on in
                        // LanguageServer.Client since we're unable to retrieve it here.
                        DocumentUri = potentialLocation.DocumentUri,
                        Range = generatedDocumentRange.ToRange()
                    };

                    csharpLocations.Add(csharpLocation);
                }
            }

            csharpFocusLocations[i] = csharpLocations.ToArray();
        }

        return csharpFocusLocations;
    }

    // Map C# code back to Razor file
    private async Task<bool> TryProcessCSharpEditAsync(
        DocumentContext documentContext,
        WorkspaceEdit csharpEdit,
        List<TextDocumentEdit> changes,
        CancellationToken cancellationToken)
    {
        using var _ = ListPool<TextDocumentEdit>.GetPooledObject(out var csharpChanges);
        if (csharpEdit.DocumentChanges is not null && csharpEdit.DocumentChanges.Value.TryGetFirst(out var documentEdits))
        {
            // We only support document edits for now. In the future once the client supports it, we should look
            // into also supporting file creation/deletion/rename.
            foreach (var edit in documentEdits)
            {
                var success = await TryProcessEditAsync(documentContext, edit.TextDocument.DocumentUri.GetRequiredParsedUri(), edit.Edits, csharpChanges, cancellationToken).ConfigureAwait(false);
                if (!success)
                {
                    return false;
                }
            }
        }

        if (csharpEdit.Changes is not null)
        {
            foreach (var edit in csharpEdit.Changes)
            {
                var generatedUri = new Uri(edit.Key);
                var success = await TryProcessEditAsync(documentContext, generatedUri, [.. edit.Value.Select(e => (SumType<TextEdit, AnnotatedTextEdit>)e)], csharpChanges, cancellationToken).ConfigureAwait(false);
                if (!success)
                {
                    return false;
                }
            }
        }

        changes.AddRange(csharpChanges);
        return true;

        async Task<bool> TryProcessEditAsync(
            DocumentContext documentContext,
            Uri generatedUri,
            SumType<TextEdit, AnnotatedTextEdit>[] textEdits,
            List<TextDocumentEdit> csharpChanges,
            CancellationToken cancellationToken)
        {
            foreach (TextEdit documentEdit in textEdits)
            {
                var (hostDocumentUri, hostDocumentRange) = await MapToHostDocumentUriAndRangeAsync(documentContext, generatedUri, documentEdit.Range.ToLinePositionSpan(), cancellationToken).ConfigureAwait(false);

                if (hostDocumentUri == generatedUri)
                {
                    return false;
                }

                var textEdit = LspFactory.CreateTextEdit(hostDocumentRange, documentEdit.NewText);

                var textDocumentEdit = new TextDocumentEdit
                {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier { DocumentUri = new(hostDocumentUri) },
                    Edits = [textEdit]
                };

                csharpChanges.Add(textDocumentEdit);
            }

            return true;
        }
    }

    // Resolve edits that are at the same start location by merging them together.
    private static void MergeEdits(List<TextDocumentEdit> changes)
    {
        var groupedChanges = changes.GroupBy(c => c.TextDocument.DocumentUri).ToImmutableArray();
        changes.Clear();
        foreach (var documentChanges in groupedChanges)
        {
            var edits = documentChanges.ToList();
            edits.Sort((x, y) => ((TextEdit)x.Edits.Single()).Range.Start.CompareTo(((TextEdit)y.Edits.Single()).Range.Start));

            for (var i = edits.Count - 1; i < edits.Count && i > 0; i--)
            {
                var previousEdit = (TextEdit)edits[i - 1].Edits.Single();
                var currentEdit = (TextEdit)edits[i].Edits.Single();
                if (currentEdit.Range.Start == previousEdit.Range.Start)
                {
                    // Append the text of the current edit to the previous edit
                    previousEdit.NewText += currentEdit.NewText;
                    previousEdit.Range.End = currentEdit.Range.End;
                    edits.RemoveAt(i);
                }
            }

            var finalEditsForDoc = new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier
                {
                    DocumentUri = documentChanges.Key,
                },
                Edits = [.. edits.SelectMany(e => e.Edits)]
            };

            changes.Add(finalEditsForDoc);
        }
    }
}
