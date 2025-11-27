// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System.Collections.Generic;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Roslyn.LanguageServer.Protocol;

internal static partial class LspExtensions
{
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

    public static WorkspaceEdit Concat(this WorkspaceEdit first, WorkspaceEdit second)
    {
        using var builder = new PooledArrayBuilder<SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>>();

        AddEdits(ref builder.AsRef(), first);
        AddEdits(ref builder.AsRef(), second);

        return new WorkspaceEdit
        {
            DocumentChanges = builder.ToArrayAndClear()
        };

        static void AddEdits(ref PooledArrayBuilder<SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>> builder, WorkspaceEdit edit)
        {
            if (edit.DocumentChanges?.Value is TextDocumentEdit[] documentEdits)
            {
                foreach (var e in documentEdits)
                {
                    builder.Add(e);
                }
            }
            else if (edit.DocumentChanges?.Value is SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[] sumTypeArray)
            {
                builder.AddRange(sumTypeArray);
            }
            else if (edit.Changes is not null)
            {
                foreach (var (uri, textEdits) in edit.Changes)
                {
                    var textDocumentEdit = new TextDocumentEdit
                    {
                        TextDocument = new OptionalVersionedTextDocumentIdentifier { DocumentUri = new(uri) },
                        Edits = [.. textEdits.Select(te => (SumType<TextEdit, AnnotatedTextEdit>)te)]
                    };
                    builder.Add(new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>(textDocumentEdit));
                }
            }
        }
    }
}
