// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentSynchronization;

[LanguageServerEndpoint(Methods.TextDocumentDidOpenName)]
internal class DocumentDidOpenEndpoint : IRazorNotificationHandler<DidOpenTextDocumentParams>
{
    public bool MutatesSolutionState => true;

    private readonly IProjectSnapshotManagerDispatcher _dispatcher;
    private readonly RazorProjectService _projectService;

    public DocumentDidOpenEndpoint(IProjectSnapshotManagerDispatcher dispatcher, RazorProjectService razorProjectService)
    {
        _dispatcher = dispatcher;
        _projectService = razorProjectService;
    }

    public async Task HandleNotificationAsync(DidOpenTextDocumentParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var sourceText = SourceText.From(request.TextDocument.Text);

        await _dispatcher.RunAsync(
            () => _projectService.OpenDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), sourceText, request.TextDocument.Version),
            cancellationToken).ConfigureAwait(false);
    }
}
