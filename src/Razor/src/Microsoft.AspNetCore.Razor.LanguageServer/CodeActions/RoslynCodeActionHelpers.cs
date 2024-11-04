// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.CodeActions;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal sealed class RoslynCodeActionHelpers(IClientConnection clientConnection) : IRoslynCodeActionHelpers
{
    private readonly IClientConnection _clientConnection = clientConnection;

    public Task<string?> GetFormattedNewFileContentsAsync(IProjectSnapshot projectSnapshot, Uri csharpFileUri, string newFileContent, CancellationToken cancellationToken)
    {
        var parameters = new FormatNewFileParams()
        {
            Project = new TextDocumentIdentifier
            {
                Uri = new Uri(projectSnapshot.FilePath, UriKind.Absolute)
            },
            Document = new TextDocumentIdentifier
            {
                Uri = csharpFileUri
            },
            Contents = newFileContent
        };
        return _clientConnection.SendRequestAsync<FormatNewFileParams, string?>(CustomMessageNames.RazorFormatNewFileEndpointName, parameters, cancellationToken);
    }

    public Task<TextEdit[]?> GetSimplifiedTextEditsAsync(DocumentContext documentContext, Uri? codeBehindUri, TextEdit edit, CancellationToken cancellationToken)
    {
        var tdi = codeBehindUri is null
            ? documentContext.GetTextDocumentIdentifierAndVersion()
            : new TextDocumentIdentifierAndVersion(new TextDocumentIdentifier() { Uri = codeBehindUri }, 1);
        var delegatedParams = new DelegatedSimplifyMethodParams(
            tdi,
            RequiresVirtualDocument: codeBehindUri == null,
            edit);

        return _clientConnection.SendRequestAsync<DelegatedSimplifyMethodParams, TextEdit[]?>(
            CustomMessageNames.RazorSimplifyMethodEndpointName,
            delegatedParams,
            cancellationToken);
    }
}
