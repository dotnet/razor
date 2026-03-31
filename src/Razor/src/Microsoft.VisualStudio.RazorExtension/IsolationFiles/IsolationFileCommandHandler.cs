// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.RazorExtension.IsolationFiles;

/// <summary>
/// Base class for handling Add/View isolation file commands for Razor documents.
/// </summary>
internal abstract class IsolationFileCommandHandler(IServiceProvider serviceProvider, string fileExtension)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly string _fileExtension = fileExtension;

    /// <summary>
    /// Gets the display text when the file doesn't exist (e.g., "Add CSS Isolation File").
    /// </summary>
    protected abstract string AddText { get; }

    /// <summary>
    /// Gets the display text when the file exists (e.g., "View CSS Isolation File").
    /// </summary>
    protected abstract string ViewText { get; }

    /// <summary>
    /// Generates the default content for a new isolation file.
    /// </summary>
    protected abstract string GenerateFileContent(string razorFilePath, string componentOrViewName);

    /// <summary>
    /// Returns whether this command is applicable for the given Razor file.
    /// Override to hide the command for certain file types.
    /// </summary>
    protected virtual bool IsApplicable(string razorFilePath) => true;

    /// <summary>
    /// Configures the command status and text based on whether the isolation file exists.
    /// </summary>
    public void OnBeforeQueryStatus(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (sender is not OleMenuCommand command)
        {
            return;
        }

        var contextGuid = RazorPackage.GuidRazorFileContext;

        // Check if the Razor file context is active before doing expensive hierarchy queries
        if (_serviceProvider.GetService(typeof(SVsShellMonitorSelection)) is not IVsMonitorSelection monitorSelection
            || monitorSelection.GetCmdUIContextCookie(ref contextGuid, out var cookie) != VSConstants.S_OK
            || monitorSelection.IsCmdUIContextActive(cookie, out var isActive) != VSConstants.S_OK
            || isActive == 0
            || GetSelectedRazorFilePath() is not string razorFilePath
            || !IsApplicable(razorFilePath))
        {
            command.Visible = false;
            return;
        }

        var isolationFilePath = GetIsolationFilePath(razorFilePath);
        var isolationFileExists = File.Exists(isolationFilePath);

        command.Visible = true;
        command.Enabled = true;
        command.Text = isolationFileExists ? ViewText : AddText;
    }

    /// <summary>
    /// Executes the command - either creates a new file or opens an existing one.
    /// </summary>
    public void Execute(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (GetSelectedRazorFilePath() is not string razorFilePath)
        {
            return;
        }

        var isolationFilePath = GetIsolationFilePath(razorFilePath);

        if (!File.Exists(isolationFilePath))
        {
            // Create the file
            CreateIsolationFile(razorFilePath, isolationFilePath);
        }

        // Open the file
        VsShellUtilities.OpenDocument(_serviceProvider, isolationFilePath);
    }

    /// <summary>
    /// Gets the path to the isolation file based on the Razor file path.
    /// </summary>
    protected virtual string GetIsolationFilePath(string razorFilePath)
    {
        return razorFilePath + _fileExtension;
    }

    /// <summary>
    /// Creates the isolation file with appropriate content.
    /// </summary>
    private void CreateIsolationFile(string razorFilePath, string isolationFilePath)
    {
        var componentName = Path.GetFileNameWithoutExtension(razorFilePath);
        var content = GenerateFileContent(razorFilePath, componentName);

        // Write the file
        File.WriteAllText(isolationFilePath, content);

        // Add to project
        AddFileToProject(isolationFilePath, razorFilePath);
    }

    /// <summary>
    /// Adds the newly created file to the project.
    /// </summary>
    private void AddFileToProject(string isolationFilePath, string razorFilePath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            if (FindProjectItem(razorFilePath) is EnvDTE.ProjectItem razorProjectItem)
            {
                // Add the isolation file as a nested item under the Razor file
                razorProjectItem.ProjectItems.AddFromFile(isolationFilePath);
            }
        }
        catch
        {
            // If adding to project fails, the file still exists on disk
            // which is better than nothing
        }
    }

    protected EnvDTE.ProjectItem? FindProjectItem(string filePath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var dte = (EnvDTE.DTE?)_serviceProvider.GetService(typeof(EnvDTE.DTE));

        return dte?.Solution.FindProjectItem(filePath);
    }

    /// <summary>
    /// Gets the file path of the currently selected Razor file in Solution Explorer.
    /// </summary>
    private string? GetSelectedRazorFilePath()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (_serviceProvider.GetService(typeof(SVsShellMonitorSelection)) is not IVsMonitorSelection monitorSelection)
        {
            return null;
        }

        monitorSelection.GetCurrentSelection(out var hierarchyPtr, out var itemId, out _, out var selectionContainerPtr);

        try
        {
            if (itemId is VSConstants.VSITEMID_NIL or VSConstants.VSITEMID_ROOT or VSConstants.VSITEMID_SELECTION)
            {
                return null;
            }

            if (hierarchyPtr != IntPtr.Zero)
            {
                var hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;
                if (hierarchy is IVsProject project)
                {
                    project.GetMkDocument(itemId, out var filePath);

                    if (!string.IsNullOrEmpty(filePath) && IsRazorFile(filePath))
                    {
                        return filePath;
                    }
                }
            }

            return null;
        }
        finally
        {
            if (hierarchyPtr != IntPtr.Zero)
            {
                Marshal.Release(hierarchyPtr);
            }

            if (selectionContainerPtr != IntPtr.Zero)
            {
                Marshal.Release(selectionContainerPtr);
            }
        }
    }

    /// <summary>
    /// Checks if a file is a Razor file (.razor or .cshtml).
    /// </summary>
    private static bool IsRazorFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".cshtml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a file is a Razor file (.razor or .cshtml).
    /// </summary>
    protected static bool IsRazorComponentFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase);
    }
}
