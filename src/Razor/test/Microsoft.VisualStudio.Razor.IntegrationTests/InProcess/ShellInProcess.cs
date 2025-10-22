﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Razor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Xunit;

namespace Microsoft.VisualStudio.Extensibility.Testing;

internal partial class ShellInProcess
{
    private static readonly ImmutableHashSet<Guid> s_windowsToClose =
    [
        FindReferencesWindowInProcess.FindReferencesWindowGuid,
        new Guid(EnvDTE.Constants.vsWindowKindObjectBrowser),
        new Guid(ToolWindowGuids80.CodedefinitionWindow),
        VSConstants.StandardToolWindows.Immediate,
    ];

    public async Task<string> GetActiveDocumentFileNameAsync(CancellationToken cancellationToken)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var monitorSelection = await GetRequiredGlobalServiceAsync<SVsShellMonitorSelection, IVsMonitorSelection>(cancellationToken);
        ErrorHandler.ThrowOnFailure(monitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_WindowFrame, out var windowFrameObj));
        var windowFrame = (IVsWindowFrame)windowFrameObj;

        ErrorHandler.ThrowOnFailure(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out var documentPathObj));
        var documentPath = (string)documentPathObj;
        return Path.GetFileName(documentPath);
    }

    public async Task SetInsertSpacesAsync(CancellationToken cancellationToken)
    {
        var textManager = await GetRequiredGlobalServiceAsync<SVsTextManager, IVsTextManager4>(cancellationToken);

        var langPrefs3 = new LANGPREFERENCES3[] { new LANGPREFERENCES3() { guidLang = RazorConstants.RazorLanguageServiceGuid } };
        Assert.Equal(VSConstants.S_OK, textManager.GetUserPreferences4(null, langPrefs3, null));

        langPrefs3[0].fInsertTabs = 0;

        Assert.Equal(VSConstants.S_OK, textManager.SetUserPreferences4(null, langPrefs3, null));
    }

    internal async Task ResetEnvironmentAsync(CancellationToken cancellationToken)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        // Make sure there is no solution open
        if (await TestServices.SolutionExplorer.IsSolutionOpenAsync(cancellationToken))
        {
            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);
            if (dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgDesignMode)
            {
                dte.Debugger.TerminateAll();
            }

            await TestServices.SolutionExplorer.CloseSolutionAndWaitAsync(cancellationToken);
        }

        await CloseActiveDocumentWindowsAsync(cancellationToken);

        await CloseActiveToolWindowsAsync(cancellationToken);

        // Our expected test results have spaces not tabs
        await TestServices.Shell.SetInsertSpacesAsync(cancellationToken);
    }

    internal async Task CloseActiveToolWindowsAsync(CancellationToken cancellationToken)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        await foreach (var window in TestServices.Shell.EnumerateWindowsAsync(__WindowFrameTypeFlags.WINDOWFRAMETYPE_Tool, cancellationToken).WithCancellation(cancellationToken))
        {
            ErrorHandler.ThrowOnFailure(window.GetGuidProperty((int)__VSFPROPID.VSFPROPID_GuidPersistenceSlot, out var persistenceSlot));
            if (s_windowsToClose.Contains(persistenceSlot))
            {
                window.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
            }
        }
    }

    internal async Task CloseActiveDocumentWindowsAsync(CancellationToken cancellationToken)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        await foreach (var window in TestServices.Shell.EnumerateWindowsAsync(__WindowFrameTypeFlags.WINDOWFRAMETYPE_Document, cancellationToken).WithCancellation(cancellationToken))
        {
            window.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
        }
    }
}
