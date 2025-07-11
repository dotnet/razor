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

    protected abstract Task<WorkspaceEdit?> TryGetCSharpMapCodeEditsAsync(DocumentContext documentContext, Guid mapCodeCorrelationId, string nodeToMapContents, LspLocation[][] focusLocations, CancellationToken cancellationToken);

    public async Task<WorkspaceEdit?> MapCodeAsync(ISolutionQueryOperations queryOperations, VSInternalMapCodeMapping[] mappings, Guid mapCodeCorrelationId, CancellationToken cancellationToken)
    {
        using var _ = ListPool<TextDocumentEdit>.GetPooledObject(out var changes);
        foreach (var mapping in mappings)
        {
            if (mapping.TextDocument is null || mapping.FocusLocations is null)
            {
                continue;
            }

            var contents = mapping.Contents;
            var testDocument = mapping.TextDocument;
            var focusLocations = mapping.FocusLocations;
            foreach (var content in contents)
            {
                if (!TryCreateDocumentContext(queryOperations, testDocument.DocumentUri.GetRequiredParsedUri(), out var documentContext))
                {
                    break;
                }

                if (content is null)
                {
                    continue;
                }

                // We create a new Razor file based on each content in each mapping order to get the syntax tree that we'll later use to map.
                var newSnapshot = documentContext.Snapshot.WithText(SourceText.From(content));
                var codeToMap = await newSnapshot.GetGeneratedOutputAsync(cancellationToken).ConfigureAwait(false);
                var syntaxTree = codeToMap.GetSyntaxTree();
                if (syntaxTree is null)
                {
                    return null;
                }

                var nodesToMap = ExtractValidNodesToMap(syntaxTree.Root);
                if (nodesToMap.Length == 0)
                {
                    return null;
                }

                var csharpFocusLocationsAndNodes = await GetCSharpFocusLocationsAndNodesAsync(queryOperations, nodesToMap, focusLocations, cancellationToken).ConfigureAwait(false);
                if (csharpFocusLocationsAndNodes is not null)
                {
                    var csharpEdits = await GetCSharpMapCodeEditsAsync(documentContext, csharpFocusLocationsAndNodes, mapCodeCorrelationId, cancellationToken).ConfigureAwait(false);

                    foreach (var edit in csharpEdits)
                    {
                        var csharpMappingSuccessful = await TryHandleDelegatedResponseAsync(documentContext, edit, changes, cancellationToken).ConfigureAwait(false);
                        if (!csharpMappingSuccessful)
                        {
                            return null;
                        }
                    }
                }

                await TryMapRazorNodesAsync(documentContext, focusLocations, nodesToMap, changes, cancellationToken).ConfigureAwait(false);
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

    private async Task<ImmutableArray<WorkspaceEdit>> GetCSharpMapCodeEditsAsync(DocumentContext documentContext, CSharpFocusLocationsAndNodes csharpFocusLocationsAndNodes, Guid mapCodeCorrelationId, CancellationToken cancellationToken)
    {
        using var csharpEdits = new PooledArrayBuilder<WorkspaceEdit>();

        foreach (var csharpBody in csharpFocusLocationsAndNodes.CSharpNodeBodies)
        {
            var edits = await TryGetCSharpMapCodeEditsAsync(documentContext, mapCodeCorrelationId, csharpBody, csharpFocusLocationsAndNodes.FocusLocations, cancellationToken).ConfigureAwait(false);
            if (edits is null)
            {
                // It's likely an error occurred during C# mapping.
                return [];
            }

            csharpEdits.Add(edits);
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

    private async Task<bool> TryMapRazorNodesAsync(
        DocumentContext documentContext,
        LspLocation[][] focusLocations,
        ImmutableArray<RazorSyntaxNode> nodesToMap,
        List<TextDocumentEdit> changes,
        CancellationToken cancellationToken)
    {
        foreach (var locationByPriority in focusLocations)
        {
            foreach (var location in locationByPriority)
            {
                Debug.Assert(location.DocumentUri.GetRequiredParsedUri() == documentContext.Uri);

                var syntaxTree = await documentContext.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                if (syntaxTree is null)
                {
                    continue;
                }

                using var razorNodesToMap = new PooledArrayBuilder<RazorSyntaxNode>();
                foreach (var nodeToMap in nodesToMap)
                {
                    // Only process new, non-C#, nodes
                    if (nodeToMap.IsCSharpNode(out _) || nodeToMap.ExistsOnTarget(syntaxTree.Root))
                    {
                        continue;
                    }

                    razorNodesToMap.Add(nodeToMap);
                }

                var sourceText = await documentContext.Snapshot.GetTextAsync(cancellationToken).ConfigureAwait(false);

                var mappingSuccess = false;
                foreach (var nodeToMap in razorNodesToMap)
                {
                    var insertionSpan = InsertMapper.GetInsertionPoint(syntaxTree.Root, sourceText, location);
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
                    return true;
                }
            }
        }

        return false;
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
    private async Task<bool> TryHandleDelegatedResponseAsync(
        DocumentContext documentContext,
        WorkspaceEdit edits,
        List<TextDocumentEdit> changes,
        CancellationToken cancellationToken)
    {
        using var _ = ListPool<TextDocumentEdit>.GetPooledObject(out var csharpChanges);
        if (edits.DocumentChanges is not null && edits.DocumentChanges.Value.TryGetFirst(out var documentEdits))
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

        if (edits.Changes is not null)
        {
            foreach (var edit in edits.Changes)
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
