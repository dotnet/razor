// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

[Export(typeof(IErrorReporter))]
[method: ImportingConstructor]
internal sealed class VisualStudioErrorReporter(SVsServiceProvider serviceProvider) : IErrorReporter
{
    private readonly SVsServiceProvider _serviceProvider = serviceProvider;

    public void ReportError(Exception exception)
    {
        if (exception is null)
        {
            return;
        }

        ThreadHelper.ThrowIfNotOnUIThread();

        if (_serviceProvider.GetService(typeof(SVsActivityLog)) is not IVsActivityLog activityLog)
        {
            return;
        }

        var hr = activityLog.LogEntry(
            (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
            "Razor Language Services",
            $"Error encountered:{Environment.NewLine}{exception}");
        ErrorHandler.ThrowOnFailure(hr);
    }

    public void ReportError(Exception exception, IProjectSnapshot? project)
    {
        if (exception is null)
        {
            return;
        }

        ThreadHelper.ThrowIfNotOnUIThread();

        if (_serviceProvider.GetService(typeof(SVsActivityLog)) is not IVsActivityLog activityLog)
        {
            return;
        }

        var hr = activityLog.LogEntry(
            (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
            "Razor Language Services",
            $"Error encountered from project '{project?.FilePath}':{Environment.NewLine}{exception}");
        ErrorHandler.ThrowOnFailure(hr);
    }

    public void ReportError(Exception exception, Project workspaceProject)
    {
        if (exception is null)
        {
            return;
        }

        ThreadHelper.ThrowIfNotOnUIThread();

        if (_serviceProvider.GetService(typeof(SVsActivityLog)) is not IVsActivityLog activityLog)
        {
            return;
        }

        var hr = activityLog.LogEntry(
            (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
            "Razor Language Services",
            $"Error encountered from project '{workspaceProject?.Name}' '{workspaceProject?.FilePath}':{Environment.NewLine}{exception}");
        ErrorHandler.ThrowOnFailure(hr);
    }
}
