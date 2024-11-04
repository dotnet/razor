// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.CodeActions;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Remote.Razor;

[Export(typeof(IRoslynCodeActionHelpers)), Shared]
internal sealed class RoslynCodeActionHelpers : IRoslynCodeActionHelpers
{
    public Task<string?> GetFormattedNewFileContentsAsync(string projectFilePath, Uri csharpFileUri, string newFileContent, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<TextEdit[]?> GetSimplifiedTextEditsAsync(Uri codeBehindUri, TextEdit edit, bool requiresVirtualDocument, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
