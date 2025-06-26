// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using WorkspacesSR = Microsoft.CodeAnalysis.Razor.Workspaces.Resources.SR;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

[Export(typeof(IIncompatibleProjectNotifier))]
[method: ImportingConstructor]
internal sealed class IncompatibleProjectNotifier(
    IProjectCapabilityResolver projectCapabilityResolver,
    ILoggerFactory loggerFactory) : IIncompatibleProjectNotifier
{
    private readonly IProjectCapabilityResolver _projectCapabilityResolver = projectCapabilityResolver;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<IncompatibleProjectNotifier>();

    public void NotifyMiscellaneousFile(TextDocument textDocument)
    {
        _logger.Log(LogLevel.Error, $"{WorkspacesSR.FormatIncompatibleProject_MiscFiles(Path.GetFileName(textDocument.FilePath))}");
    }

    public void NotifyNullDocument(Project project, string filePath)
    {
        // When this document was opened, we will have checked if it was a .NET Framework project. If so, then we can avoid
        // notifying the user because they are not using the LSP editor, even though we get the odd request.
        // If this check returns a false positive, the fallout is only one log message, so nothing to be concerned about.
        if (_projectCapabilityResolver.TryGetCachedCapabilityMatch(project.FilePath.AssumeNotNull(), WellKnownProjectCapabilities.DotNetCoreCSharp, out var isMatch) && !isMatch)
        {
            return;
        }

        _logger.Log(LogLevel.Error, $"{(
            project.AdditionalDocuments.Any(d => d.FilePath is not null && d.FilePath.IsRazorFilePath())
                ? WorkspacesSR.FormatIncompatibleProject_NotAnAdditionalFile(Path.GetFileName(filePath), project.Name)
                : WorkspacesSR.FormatIncompatibleProject_NoAdditionalFiles(Path.GetFileName(filePath), project.Name))}");
    }
}
