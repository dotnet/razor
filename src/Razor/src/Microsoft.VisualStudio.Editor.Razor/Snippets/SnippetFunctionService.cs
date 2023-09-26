// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.VisualStudio.Editor.Razor.Snippets;

internal static class SnippetFunctionService
{
    /// <summary>
    /// Gets the name of the class that contains the specified position.
    /// </summary>
    public static async Task<string?> GetContainingClassNameAsync(Document document, int position, CancellationToken cancellationToken)
    {
        // Find the nearest enclosing type declaration and use its name
        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        syntaxTree.AssumeNotNull();

        var root = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        root.AssumeNotNull();

        var type = root.FindToken(position).Parent?.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        return type?.Identifier.ToString();
    }
}
