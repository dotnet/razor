// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal interface IAutoInsertService
{
    ImmutableArray<string> TriggerCharacters { get; }

    VSInternalDocumentOnAutoInsertResponseItem? TryResolveInsertion(
        RazorCodeDocument codeDocument,
        Position position,
        string character,
        bool autoCloseTags
    );
}
