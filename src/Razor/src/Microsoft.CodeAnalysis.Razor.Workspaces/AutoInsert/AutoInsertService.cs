// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal class AutoInsertService(IEnumerable<IOnAutoInsertProvider> onAutoInsertProviders) : IAutoInsertService
{
    private readonly IEnumerable<IOnAutoInsertProvider> _onAutoInsertProviders = onAutoInsertProviders;

    public static HashSet<string> HtmlAllowedAutoInsertTriggerCharacters { get; } = new(StringComparer.Ordinal) { "=", };
    public static HashSet<string> CSharpAllowedAutoInsertTriggerCharacters { get; } = new(StringComparer.Ordinal) { "'", "/", "\n" };

    // This gets called just once
    public IEnumerable<string> TriggerCharacters => _onAutoInsertProviders.Select((provider) => provider.TriggerCharacter);

    public InsertTextEdit? TryResolveInsertion(
        RazorCodeDocument codeDocument,
        Position position,
        string character,
        bool autoCloseTags)
    {
        using var applicableProviders = new PooledArrayBuilder<IOnAutoInsertProvider>();
        foreach (var provider in _onAutoInsertProviders)
        {
            if (provider.TriggerCharacter == character)
            {
                applicableProviders.Add(provider);
            }
        }

        if (applicableProviders.Count == 0)
        {
            // There's currently a bug in the LSP platform where other language clients OnAutoInsert trigger characters influence every language clients trigger characters.
            // To combat this we need to preemptively return so we don't try having our providers handle characters that they can't.
            return null;
        }

        foreach (var provider in applicableProviders)
        {
            var insertTextEdit = provider.TryResolveInsertion(
                position,
                codeDocument,
                autoCloseTags
            );

            if (insertTextEdit is not null)
            {
                return insertTextEdit;
            }
        }

        // No provider could handle the text edit.
        return null;
    }
}
