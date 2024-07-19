// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal interface IOnAutoInsertProvider
{
    string TriggerCharacter { get; }

    public ValueTask<InsertTextEdit?> TryResolveInsertionAsync(Position position, IDocumentSnapshot documentSnapshot, bool autoClosingTagsOption);
}
