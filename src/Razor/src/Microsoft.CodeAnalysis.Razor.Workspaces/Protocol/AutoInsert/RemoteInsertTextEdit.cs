// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert;

[DataContract]
internal readonly record struct RemoteInsertTextEdit(
        [property: DataMember(Order = 0)]
        LinePositionSpan LinePositionSpan,
        [property: DataMember(Order = 1)]
        string NewText,
        [property: DataMember(Order = 2)]
        InsertTextFormat InsertTextFormat
    )
{
    public static RemoteInsertTextEdit FromLspInsertTextEdit(InsertTextEdit edit)
        => new (
            edit.TextEdit.Range.ToLinePositionSpan(),
            edit.TextEdit.NewText,
            edit.InsertTextFormat);

    public static VSInternalDocumentOnAutoInsertResponseItem ToLspInsertTextEdit(RemoteInsertTextEdit edit)
        => new()
        {
            TextEdit = new()
            {
                Range = edit.LinePositionSpan.ToRange(),
                NewText = edit.NewText
            },
            TextEditFormat = edit.InsertTextFormat,
        };

    public override string ToString()
    {
        return $"({LinePositionSpan.Start.Line}, {LinePositionSpan.Start.Character})-({LinePositionSpan.End.Line}, {LinePositionSpan.End.Character}), '{NewText}', {InsertTextFormat}";
    }
}
