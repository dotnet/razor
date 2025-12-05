// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using System.IO;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Razor.LanguageClient;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor.ProjectSystem;

[Order(RazorConstants.AboveManagedProjectSystemOrder)]
[Export(typeof(IProjectTreeActionHandler))]
[AppliesTo(WellKnownProjectCapabilities.DotNetCoreCSharp)]
[method: ImportingConstructor]
internal sealed class RenamerProjectTreeActionHandler(
    [Import(ExportContractNames.Scopes.UnconfiguredProject)] IProjectAsynchronousTasksService projectAsynchronousTasksService,
    LSPRequestInvoker requestInvoker,
    LanguageServerFeatureOptions featureOptions,
    ILoggerFactory loggerFactory) : ProjectTreeActionHandlerBase
{
    private readonly IProjectAsynchronousTasksService _projectAsynchronousTasksService = projectAsynchronousTasksService;
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;
    private readonly LanguageServerFeatureOptions _featureOptions = featureOptions;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<RenamerProjectTreeActionHandler>();

    public override async Task RenameAsync(IProjectTreeActionHandlerContext context, IProjectTree node, string value)
    {
        ApplyRenameEditParams? request = null;
        try
        {
            // Only supported in cohosting
            if (!_featureOptions.UseRazorCohostServer)
            {
                return;
            }

            if (node.FilePath is null || node.IsFolder)
            {
                return;
            }

            var oldFilePath = node.FilePath;
            var newFilePath = Path.Combine(Path.GetDirectoryName(oldFilePath), value);

            // We only do fancy renames for Razor component files, and only if they're not changing file extensions
            if (!FileUtilities.IsRazorComponentFilePath(oldFilePath, PathUtilities.OSSpecificPathComparison) ||
                !FileUtilities.IsRazorComponentFilePath(newFilePath, PathUtilities.OSSpecificPathComparison))
            {
                return;
            }

            var response = await _projectAsynchronousTasksService.LoadedProjectAsync(() => _requestInvoker.ReinvokeRequestOnServerAsync<RenameFilesParams, WorkspaceEdit?>(
                Methods.WorkspaceWillRenameFilesName,
                RazorLSPConstants.RoslynLanguageServerName,
                new RenameFilesParams()
                {
                    Files =
                    [
                        new FileRename()
                    {
                        OldUri = new(RazorUri.CreateAbsoluteUri(oldFilePath)),
                        NewUri = new(RazorUri.CreateAbsoluteUri(newFilePath)),
                    }
                    ]
                },
                _projectAsynchronousTasksService.UnloadCancellationToken));

            if (response.Result is null)
            {
                return;
            }

            request = new ApplyRenameEditParams
            {
                Edit = response.Result,
                OldFilePath = oldFilePath,
                NewFilePath = newFilePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during rename operation.");
        }
        finally
        {
            // Always perform the default rename operation (renaming the file on disk)
            await base.RenameAsync(context, node, value);
        }

        if (request is null)
        {
            return;
        }

        ApplyWorkspaceEditAsync(request).Forget();
    }

    private async Task ApplyWorkspaceEditAsync(ApplyRenameEditParams request)
    {
        // We want to let the rename operation finish to avoid deadlocks
        await Task.Yield();

        await _projectAsynchronousTasksService.LoadedProjectAsync(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            await _requestInvoker.ReinvokeRequestOnServerAsync<ApplyRenameEditParams, VoidResult>(
                 RazorLSPConstants.ApplyRenameEditName,
                 RazorLSPConstants.RoslynLanguageServerName,
                 request,
                 _projectAsynchronousTasksService.UnloadCancellationToken);
        });
    }
}
