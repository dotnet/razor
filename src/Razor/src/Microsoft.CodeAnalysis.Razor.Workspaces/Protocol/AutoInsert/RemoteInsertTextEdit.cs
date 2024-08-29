﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert;

// TODO: check annotations
[DataContract]
internal readonly record struct RemoteInsertTextEdit(
        [property: DataMember(Name = "textEdit")]
        TextEdit TextEdit,
        [property: DataMember(Name = "insertTextFormat")]
        InsertTextFormat InsertTextFormat
    )
{
    public static RemoteInsertTextEdit FromLspInsertTextEdit(InsertTextEdit edit)
        => new (edit.TextEdit, edit.InsertTextFormat);
}
