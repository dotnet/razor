// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using WorkspacesSR = Microsoft.CodeAnalysis.Razor.Workspaces.Resources.SR;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[Export(typeof(IIncompatibleProjectService))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class IncompatibleProjectService(
    ILoggerFactory loggerFactory) : IIncompatibleProjectService
{
    private static readonly ProjectId s_miscFilesProject = ProjectId.CreateNewId();

    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<IncompatibleProjectService>();

    private ImmutableHashSet<ProjectId> _incompatibleProjectIds = [];

    public void HandleMiscellaneousFile(TextDocument textDocument)
    {
        if (ImmutableInterlocked.Update(ref _incompatibleProjectIds, static set => set.Add(s_miscFilesProject)))
        {
            _logger.Log(LogLevel.Error, $"{WorkspacesSR.FormatIncompatibleProject_MiscFiles(Path.GetFileName(textDocument.FilePath))}");
        }
    }

    public void HandleNullDocument(RazorTextDocumentIdentifier? textDocumentIdentifier, RazorCohostRequestContext context)
    {
        if (context.Solution is null)
        {
            // If the solution is null, we have no idea what is going on, so err on the side of ignoring this request
            // and not annoying the user.
            return;
        }

        if (textDocumentIdentifier is not { Uri: { } uri })
        {
            // Can't do anything without a uri
            return;
        }

        // We know that the textDocumentIdentifier doesn't map to a document in the solution, or we wouldn't be here,
        // but we don't want to notify the user for each file, so we try to find the project that contains the file
        // through other means.

        var filePath = uri.GetDocumentFilePath();
        var filePathSpan = filePath.AsSpan();
        foreach (var project in context.Solution.Projects)
        {
            if (project.FilePath is null)
            {
                continue;
            }

            if (filePathSpan.StartsWith(PathUtilities.GetDirectoryName(project.FilePath.AsSpan()), PathUtilities.OSSpecificPathComparison))
            {
                ReportNullDocument(project, filePath);
                return;
            }
        }

        // If we couldn't find a candidate project, then this could be a misc file or linked file from somewhere, but we'll err on the side of not reporting
        // it. In future we could consider a separate hashset for these, so we report once per file.
    }

    private void ReportNullDocument(Project project, string filePath)
    {
        if (ImmutableInterlocked.Update(ref _incompatibleProjectIds, static (set, id) => set.Add(id), project.Id))
        {
            // TODO: In VS, should we abstract these notification out so we can show an info bar?
            _logger.Log(LogLevel.Error, $"{(
                project.AdditionalDocuments.Any(d => d.FilePath is not null && d.FilePath.IsRazorFilePath())
                    ? WorkspacesSR.FormatIncompatibleProject_NotAnAdditionalFile(Path.GetFileName(filePath), project.Name)
                    : WorkspacesSR.FormatIncompatibleProject_NoAdditionalFiles(Path.GetFileName(filePath), project.Name))}");
        }
    }
}
