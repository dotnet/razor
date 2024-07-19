// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal class AutoInsertService(IEnumerable<IOnAutoInsertProvider> onAutoInsertProviders) : IAutoInsertService
{
    private readonly IEnumerable<IOnAutoInsertProvider> _onAutoInsertProviders = onAutoInsertProviders;

    // This gets called just once
    public IEnumerable<string> TriggerCharacters => _onAutoInsertProviders.Select((provider) => provider.TriggerCharacter);

    public async ValueTask<InsertTextEdit?> TryResolveInsertionAsync(
        IDocumentSnapshot documentSnapshot,
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
            var insertTextEdit = await provider.TryResolveInsertionAsync(
                position,
                documentSnapshot,
                autoCloseTags
            ).ConfigureAwait(false);

            if (insertTextEdit is not null)
            {
                return insertTextEdit;
            }
        }

        // No provider could handle the text edit.
        return null;
    }
}
