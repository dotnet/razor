﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Design;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.RazorExtension.SyntaxVisualizer;

/// <summary>
/// This class implements the tool window exposed by this package and hosts a user control.
///
/// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane, 
/// usually implemented by the package implementer.
///
/// This class derives from the ToolWindowPane class provided from the MPF in order to use its 
/// implementation of the IVsUIElementPane interface.
/// </summary>
[Guid("28080d9c-0842-4155-9e7d-3b9e6d64bb29")]
internal class SyntaxVisualizerToolWindow : ToolWindowPane
{
    // Values from SyntaxVisualizerMenu.vsct
    private static readonly Guid CmdSet = new Guid("a3a603a2-2b17-4ce2-bd21-cbb8ccc084ec");
    private const int ToolbarCmdId = 0x0102;
    private const int CmdIdShowSourceMappingsButton = 0x0110;
    private const int CmdIdShowGeneratedCode = 0x0111;
    private const int CmdIdShowGeneratedHtml = 0x0112;

    private MenuCommand? _showSourceMappingsCommand;
    private SyntaxVisualizerControl _visualizerControl => (SyntaxVisualizerControl)Content;

    public bool CommandHandlersInitialized { get; private set; }

    /// <summary>
    /// Standard constructor for the tool window.
    /// </summary>
    public SyntaxVisualizerToolWindow()
        : base(null)
    {
        // Set the window title reading it from the resources.
        Caption = VSPackage.RazorSyntaxVisualizer;

        // Set the image that will appear on the tab of the window frame
        // when docked with an other window
        // The resource ID correspond to the one defined in the resx file
        // while the Index is the offset in the bitmap strip. Each image in
        // the strip being 16x16.
        BitmapResourceID = 500;
        BitmapIndex = 0;

        // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
        // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on 
        // the object returned by the Content property.
        Content = new SyntaxVisualizerControl();

        ToolBar = new CommandID(CmdSet, ToolbarCmdId);
        ToolBarLocation = (int)VSTWT_LOCATION.VSTWT_TOP;
    }

    internal TServiceInterface GetVsService<TServiceInterface, TService>()
        where TServiceInterface : class
        where TService : class
    {
        return (TServiceInterface)GetService(typeof(TService));
    }

    internal void InitializeCommands(IMenuCommandService mcs, Guid guidSyntaxVisualizerMenuCmdSet)
    {
        Contract.Requires(!CommandHandlersInitialized);

        CommandHandlersInitialized = true;

        _showSourceMappingsCommand = new MenuCommand(ShowSourceMappings, new CommandID(guidSyntaxVisualizerMenuCmdSet, CmdIdShowSourceMappingsButton))
        {
            Checked = SourceMappingTagger.Enabled
        };

        mcs.AddCommand(_showSourceMappingsCommand);
        mcs.AddCommand(new MenuCommand(ShowGeneratedCode, new CommandID(guidSyntaxVisualizerMenuCmdSet, CmdIdShowGeneratedCode)));
        mcs.AddCommand(new MenuCommand(ShowGeneratedHtml, new CommandID(guidSyntaxVisualizerMenuCmdSet, CmdIdShowGeneratedHtml)));

    }

    private void ShowSourceMappings(object sender, EventArgs e)
    {
        _visualizerControl.ShowSourceMappings();

        if (_showSourceMappingsCommand is not null)
        {
            // Always update the checked state after executing the handler
            _showSourceMappingsCommand.Checked = SourceMappingTagger.Enabled;
        }
    }

    private void ShowGeneratedCode(object sender, EventArgs e)
    {
        _visualizerControl.ShowGeneratedCode();
    }

    private void ShowGeneratedHtml(object sender, EventArgs e)
    {
        _visualizerControl.ShowGeneratedHtml();
    }
}
