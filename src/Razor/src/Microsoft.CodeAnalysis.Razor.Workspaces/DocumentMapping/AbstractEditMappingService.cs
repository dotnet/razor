// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

internal abstract class AbstractEditMappingService(
    IDocumentMappingService documentMappingService,
    IFilePathService filePathService) : IEditMappingService
{
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;
    private readonly IFilePathService _filePathService = filePathService;

    public async Task<WorkspaceEdit> RemapWorkspaceEditAsync(IDocumentSnapshot contextDocumentSnapshot, WorkspaceEdit workspaceEdit, CancellationToken cancellationToken)
    {
        // Handle DocumentChanges - we need to preserve CreateFile, RenameFile, DeleteFile operations
        if (workspaceEdit.DocumentChanges is { } documentChanges)
        {
            var remappedDocumentChanges = await RemapDocumentChangesAsync(contextDocumentSnapshot, documentChanges, cancellationToken).ConfigureAwait(false);

            return new WorkspaceEdit()
            {
                DocumentChanges = remappedDocumentChanges
            };
        }

        if (workspaceEdit.Changes is { } changeMap)
        {
            var remappedEdits = await RemapDocumentEditsAsync(contextDocumentSnapshot, changeMap, cancellationToken).ConfigureAwait(false);

            return new WorkspaceEdit()
            {
                Changes = remappedEdits
            };
        }

        return workspaceEdit;
    }

    private async Task<Dictionary<string, TextEdit[]>> RemapDocumentEditsAsync(IDocumentSnapshot contextDocumentSnapshot, Dictionary<string, TextEdit[]> changes, CancellationToken cancellationToken)
    {
        var remappedChanges = new Dictionary<string, TextEdit[]>(capacity: changes.Count);

        foreach (var (uriString, edits) in changes)
        {
            var generatedDocumentUri = new Uri(uriString);

            // For Html we just map the Uri, the range will be the same
            if (_filePathService.IsVirtualHtmlFile(generatedDocumentUri))
            {
                var razorUri = _filePathService.GetRazorDocumentUri(generatedDocumentUri);
                remappedChanges[razorUri.AbsoluteUri] = edits;
            }

            // Check if the edit is actually for a generated document, because if not we don't need to do anything
            if (!_filePathService.IsVirtualCSharpFile(generatedDocumentUri))
            {
                remappedChanges[uriString] = edits;
                continue;
            }

            var razorDocumentUri = await GetRazorDocumentUriAsync(contextDocumentSnapshot, generatedDocumentUri, cancellationToken).ConfigureAwait(false);
            if (razorDocumentUri is null)
            {
                continue;
            }

            if (!TryGetDocumentContext(contextDocumentSnapshot, razorDocumentUri, projectContext: null, out var documentContext))
            {
                continue;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var remappedEdits = RemapTextEditsCore(codeDocument.GetRequiredCSharpDocument(), edits);
            if (remappedEdits.Length == 0)
            {
                // Nothing to do.
                continue;
            }

            remappedChanges[razorDocumentUri.AbsoluteUri] = remappedEdits;
        }

        return remappedChanges;
    }

    private async Task<SumType<TextDocumentEdit[], SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]>> RemapDocumentChangesAsync(
        IDocumentSnapshot contextDocumentSnapshot,
        SumType<TextDocumentEdit[], SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]> documentChanges,
        CancellationToken cancellationToken)
    {
        // If it's just TextDocumentEdit[], remap and return
        if (documentChanges.Value is TextDocumentEdit[] textDocumentEdits)
        {
            return await RemapTextDocumentEditsAsync(contextDocumentSnapshot, textDocumentEdits, cancellationToken).ConfigureAwait(false);
        }

        // If it's SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[], we need to preserve non-TextDocumentEdit operations
        if (documentChanges.Value is SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[] sumTypeArray)
        {
            using var result = new PooledArrayBuilder<SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>>(sumTypeArray.Length);

            foreach (var item in sumTypeArray)
            {
                if (item.Value is TextDocumentEdit textDocumentEdit)
                {
                    // Remap the TextDocumentEdit using extracted method to avoid array allocation
                    var remapped = await TryRemapSingleTextDocumentEditAsync(contextDocumentSnapshot, textDocumentEdit, cancellationToken).ConfigureAwait(false);
                    if (remapped is not null)
                    {
                        result.Add(remapped);
                    }
                }
                else
                {
                    // Preserve CreateFile, RenameFile, DeleteFile operations as-is
                    result.Add(item);
                }
            }

            return result.ToArray();
        }

        return Assumed.Unreachable<SumType<TextDocumentEdit[], SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]>>();
    }

    private TextEdit[] RemapTextEditsCore(RazorCSharpDocument csharpDocument, TextEdit[] edits)
    {
        using var remappedEdits = new PooledArrayBuilder<TextEdit>(edits.Length);

        foreach (var edit in edits)
        {
            var generatedRange = edit.Range;
            if (!_documentMappingService.TryMapToRazorDocumentRange(csharpDocument, generatedRange, MappingBehavior.Strict, out var hostDocumentRange))
            {
                // Can't map range. Discard this edit.
                continue;
            }

            var remappedEdit = LspFactory.CreateTextEdit(hostDocumentRange, edit.NewText);
            remappedEdits.Add(remappedEdit);
        }

        return remappedEdits.ToArray();
    }

    private async Task<TextDocumentEdit[]> RemapTextDocumentEditsAsync(IDocumentSnapshot contextDocumentSnapshot, TextDocumentEdit[] documentEdits, CancellationToken cancellationToken)
    {
        using var remappedDocumentEdits = new PooledArrayBuilder<TextDocumentEdit>(documentEdits.Length);

        foreach (var entry in documentEdits)
        {
            var remapped = await TryRemapSingleTextDocumentEditAsync(contextDocumentSnapshot, entry, cancellationToken).ConfigureAwait(false);
            if (remapped is not null)
            {
                remappedDocumentEdits.Add(remapped);
            }
        }

        return remappedDocumentEdits.ToArray();
    }

    private async Task<TextDocumentEdit?> TryRemapSingleTextDocumentEditAsync(IDocumentSnapshot contextDocumentSnapshot, TextDocumentEdit entry, CancellationToken cancellationToken)
    {
        var generatedDocumentUri = entry.TextDocument.DocumentUri.GetRequiredParsedUri();

        // For Html we just map the Uri, the range will be the same
        if (_filePathService.IsVirtualHtmlFile(generatedDocumentUri))
        {
            var razorUri = _filePathService.GetRazorDocumentUri(generatedDocumentUri);
            entry.TextDocument = new OptionalVersionedTextDocumentIdentifier()
            {
                DocumentUri = new(razorUri),
            };
            return entry;
        }

        // Check if the edit is actually for a generated document, because if not we don't need to do anything
        if (!_filePathService.IsVirtualCSharpFile(generatedDocumentUri))
        {
            // This location doesn't point to a background razor file. No need to remap.
            return entry;
        }

        var razorDocumentUri = await GetRazorDocumentUriAsync(contextDocumentSnapshot, generatedDocumentUri, cancellationToken).ConfigureAwait(false);
        if (razorDocumentUri is null)
        {
            return null;
        }

        if (!TryGetDocumentContext(contextDocumentSnapshot, razorDocumentUri, entry.TextDocument.GetProjectContext(), out var documentContext))
        {
            return null;
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        // entry.Edits is SumType<TextEdit, AnnotatedTextEdit> but AnnotatedTextEdit inherits from TextEdit, so we can just cast
        var remappedEdits = RemapTextEditsCore(codeDocument.GetRequiredCSharpDocument(), [.. entry.Edits.Select(static e => (TextEdit)e)]);
        if (remappedEdits.Length == 0)
        {
            // Nothing to do.
            return null;
        }

        return new()
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier()
            {
                DocumentUri = new(razorDocumentUri),
            },
            Edits = [.. remappedEdits.Select(static e => new SumType<TextEdit, AnnotatedTextEdit>(e))]
        };
    }

    protected abstract bool TryGetDocumentContext(IDocumentSnapshot contextDocumentSnapshot, Uri razorDocumentUri, VSProjectContext? projectContext, [NotNullWhen(true)] out DocumentContext? documentContext);

    protected abstract Task<Uri?> GetRazorDocumentUriAsync(IDocumentSnapshot contextDocumentSnapshot, Uri uri, CancellationToken cancellationToken);
}
