// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal interface IAutoInsertService
{
    IEnumerable<string> TriggerCharacters { get; }

    ValueTask<InsertTextEdit?> TryResolveInsertionAsync(
        IDocumentSnapshot documentSnapshot,
        Position position,
        string character,
        bool autoCloseTags,
        CancellationToken cancellationToken
    );
}
