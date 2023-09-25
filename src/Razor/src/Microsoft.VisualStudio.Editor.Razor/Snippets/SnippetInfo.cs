// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Editor.Razor.Snippets;

internal record SnippetInfo(string Shortcut, string Title, string Description, string Path, SnippetLanguage Language)
{
    private string? _lspInsertionText;

    internal async Task<string?> TryGetLSPInsertionTextAsync(CancellationToken cancellationToken)
    {
        if (_lspInsertionText is not null)
        {
            return _lspInsertionText;
        }

        try
        {
            var text = File.ReadAllText(Path);
            cancellationToken.ThrowIfCancellationRequested();

            _lspInsertionText = await GetLSPInsertionFromSnippetTextAsync(text, cancellationToken).ConfigureAwait(false);
            return _lspInsertionText;
        }
        catch
        {
            return null;
        }
    }

    private static Task<string?> GetLSPInsertionFromSnippetTextAsync(string text, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(text);
    }
}

internal enum SnippetLanguage
{
    CSharp,
    Html,
    Razor
}
