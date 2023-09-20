// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.Editor.Razor.Snippets;

internal record SnippetInfo(string Shortcut, string Title, string Description, string Path, SnippetLanguage Language)
{
}

internal enum SnippetLanguage
{
    CSharp,
    Html,
    Razor
}
