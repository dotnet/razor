﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

[Export(typeof(IIncompatibleProjectNotifier))]
[method: ImportingConstructor]
internal sealed class IncompatibleProjectNotifier(ILoggerFactory loggerFactory) : IIncompatibleProjectNotifier
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<IncompatibleProjectNotifier>();

    public void NotifyMiscFilesDocument(TextDocument textDocument)
    {
        _logger.Log(LogLevel.Error, $"{WorkspacesSR.FormatIncompatibleProject_MiscFiles(Path.GetFileName(textDocument.FilePath))}");
    }

    public void NotifyMissingDocument(Project project, string filePath)
    {
        _logger.Log(LogLevel.Error, $"{(
            project.AdditionalDocuments.Any(d => d.FilePath is not null && d.FilePath.IsRazorFilePath())
                ? WorkspacesSR.FormatIncompatibleProject_NotAnAdditionalFile(Path.GetFileName(filePath), project.Name)
                : WorkspacesSR.FormatIncompatibleProject_NoAdditionalFiles(Path.GetFileName(filePath), project.Name))}");
    }
}
