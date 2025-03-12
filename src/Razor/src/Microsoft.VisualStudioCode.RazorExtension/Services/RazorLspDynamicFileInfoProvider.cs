// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

[Export(typeof(IRazorLspDynamicFileInfoProvider)), Shared]
internal sealed class RazorLspDynamicFileInfoProvider(IRazorClientLanguageServerManager clientLanguageServerManager, IWorkspaceProvider workspaceProvider) : IRazorLspDynamicFileInfoProvider
{
    private const string ProvideRazorDynamicFileInfoMethodName = "razor/provideDynamicFileInfo";

    private readonly IRazorClientLanguageServerManager _clientLanguageServerManager = clientLanguageServerManager;
    private readonly IWorkspaceProvider _workspaceProvider = workspaceProvider;

    public event EventHandler<string>? Updated;

    public async Task<RazorDynamicFileInfo?> GetDynamicFileInfoAsync(ProjectId projectId, string? projectFilePath, string filePath, CancellationToken cancellationToken)
    {
        var razorUri = new Uri(filePath);

        var requestParams = new RazorProvideDynamicFileParams
        {
            RazorDocument = new()
            {
                Uri = razorUri
            }
        };

        var response = await _clientLanguageServerManager.SendRequestAsync<RazorProvideDynamicFileParams, RazorProvideDynamicFileResponse>(
            ProvideRazorDynamicFileInfoMethodName,
            requestParams,
            cancellationToken).ConfigureAwait(false);

        if (response.Updates is null)
        {
            return null;
        }

        var workspace = _workspaceProvider.GetWorkspace();
        var textDocument = await WorkspaceExtensions.GetTextDocumentAsync(workspace, response.CSharpDocument.Uri, cancellationToken).ConfigureAwait(false);
        var checksum = Convert.FromBase64String(response.Checksum);
        var textLoader = new LspTextChangesTextLoader(
            textDocument,
            response.Updates,
            checksum,
            response.ChecksumAlgorithm,
            response.SourceEncodingCodePage,
            razorUri,
            _clientLanguageServerManager);

        return new RazorDynamicFileInfo(
            response.CSharpDocument.Uri.ToString(),
            SourceCodeKind.Regular,
            textLoader,
            documentServiceProvider: EmptyServiceProvider.Instance);
    }

    public Task RemoveDynamicFileInfoAsync(ProjectId projectId, string? projectFilePath, string filePath, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Update(Uri razorUri)
    {
        Updated?.Invoke(this, razorUri.ToString());
    }
}

