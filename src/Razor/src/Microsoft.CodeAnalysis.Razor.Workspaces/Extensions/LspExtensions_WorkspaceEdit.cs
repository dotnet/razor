// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Roslyn.LanguageServer.Protocol;

internal static partial class LspExtensions
{
    /// <summary>
    /// Gets the <see cref="TextDocumentEdit"/> objects from the <see cref="WorkspaceEdit.DocumentChanges"/> property.
    /// </summary>
    /// <remarks>
    /// WARNING: This method only yields <see cref="TextDocumentEdit"/> objects. If the <see cref="WorkspaceEdit"/>
    /// contains <see cref="CreateFile"/>, <see cref="RenameFile"/>, or <see cref="DeleteFile"/> operations,
    /// they will NOT be included. Be careful not to create a new <see cref="WorkspaceEdit"/> with just the
    /// results of this method, as doing so would lose those operations and could lead to data loss.
    /// </remarks>
    public static IEnumerable<TextDocumentEdit> GetTextDocumentEdits(this WorkspaceEdit workspaceEdit)
    {
        if (workspaceEdit.DocumentChanges?.Value is TextDocumentEdit[] documentEdits)
        {
            foreach (var edit in documentEdits)
            {
                yield return edit;
            }
        }
        else if (workspaceEdit.DocumentChanges?.Value is SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[] sumTypeArray)
        {
            foreach (var sumType in sumTypeArray)
            {
                if (sumType.Value is TextDocumentEdit textDocumentEdit)
                {
                    yield return textDocumentEdit;
                }
            }
        }
    }

    /// <summary>
    /// Tries to get the <see cref="TextDocumentEdit"/> objects from the <see cref="WorkspaceEdit.DocumentChanges"/> property.
    /// </summary>
    /// <remarks>
    /// WARNING: This method only returns <see cref="TextDocumentEdit"/> objects. If the <see cref="WorkspaceEdit"/>
    /// contains <see cref="CreateFile"/>, <see cref="RenameFile"/>, or <see cref="DeleteFile"/> operations,
    /// they will NOT be included. Be careful not to create a new <see cref="WorkspaceEdit"/> with just the
    /// results of this method, as doing so would lose those operations and could lead to data loss.
    /// </remarks>
    public static bool TryGetTextDocumentEdits(this WorkspaceEdit workspaceEdit, [NotNullWhen(true)] out TextDocumentEdit[]? textDocumentEdits)
    {
        if (workspaceEdit.DocumentChanges?.Value is TextDocumentEdit[] documentEdits)
        {
            textDocumentEdits = documentEdits;
            return true;
        }

        if (workspaceEdit.DocumentChanges?.Value is SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[] sumTypeArray)
        {
            using var builder = new PooledArrayBuilder<TextDocumentEdit>();
            foreach (var sumType in sumTypeArray)
            {
                if (sumType.Value is TextDocumentEdit textDocumentEdit)
                {
                    builder.Add(textDocumentEdit);
                }
            }

            if (builder.Count > 0)
            {
                textDocumentEdits = builder.ToArray();
                return true;
            }
        }

        textDocumentEdits = null;
        return false;
    }
}
