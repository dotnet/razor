// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.CodeActions;

internal interface IRoslynCodeActionHelpers
{
    Task<string> GetFormattedNewFileContentsAsync(IProjectSnapshot projectSnapshot, DocumentUri csharpFileUri, string newFileContent, CancellationToken cancellationToken);

    /// <summary>
    /// Apply the edit to the specified document, get Roslyn to simplify it, and return the simplified edit
    /// </summary>
    /// <param name="documentContext">The Razor document context for the edit</param>
    /// <param name="codeBehindUri">If present, the Roslyn document to apply the edit to. Otherwise the generated C# document will be used</param>
    /// <param name="edit">The edit to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TextEdit[]?> GetSimplifiedTextEditsAsync(DocumentContext documentContext, DocumentUri? codeBehindUri, TextEdit edit, CancellationToken cancellationToken);
}
