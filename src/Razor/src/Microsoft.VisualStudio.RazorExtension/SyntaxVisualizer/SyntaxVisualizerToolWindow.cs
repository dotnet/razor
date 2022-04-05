// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.RazorExtension.SyntaxVisualizer
{
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
    public class SyntaxVisualizerToolWindow : ToolWindowPane
    {
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
        }

        internal TServiceInterface GetVsService<TServiceInterface, TService>()
            where TServiceInterface : class
            where TService : class
        {
            return (TServiceInterface)GetService(typeof(TService));
        }
    }
}
