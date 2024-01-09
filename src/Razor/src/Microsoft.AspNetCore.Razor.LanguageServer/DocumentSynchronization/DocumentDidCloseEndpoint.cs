// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentSynchronization;

[LanguageServerEndpoint(Methods.TextDocumentDidCloseName)]
internal class DocumentDidCloseEndpoint : IRazorNotificationHandler<DidCloseTextDocumentParams>, ITextDocumentIdentifierHandler<DidCloseTextDocumentParams, TextDocumentIdentifier>
{
    private readonly IProjectSnapshotManagerDispatcher _dispatcher;
    private readonly RazorProjectService _projectService;

    public bool MutatesSolutionState => true;

    public DocumentDidCloseEndpoint(
        IProjectSnapshotManagerDispatcher dispatcher,
        RazorProjectService projectService)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(DidCloseTextDocumentParams request)
    {
        return request.TextDocument;
    }

    public async Task HandleNotificationAsync(DidCloseTextDocumentParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        await _dispatcher.RunAsync(
            () => _projectService.CloseDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath()),
            cancellationToken).ConfigureAwait(false);
    }
}
