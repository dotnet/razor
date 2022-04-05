// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.VisualStudio.RazorExtension.SyntaxVisualizer
{
    internal static class Helpers
    {
        internal static IServiceProvider? _globalServiceProvider;

        internal static IServiceProvider GlobalServiceProvider
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (_globalServiceProvider == null)
                {
                    _globalServiceProvider = (IServiceProvider)Package.GetGlobalService(typeof(IServiceProvider));
                }

                return _globalServiceProvider;
            }
        }

        internal static TServiceInterface GetRequiredMefService<TServiceInterface, TService>()
            where TServiceInterface : class
            where TService : class
        {
            var service = (TServiceInterface?)GetService(GlobalServiceProvider, typeof(TService).GUID, false);
            Assumes.Present(service);
            return service;
        }
        internal static TServiceInterface GetRequiredMefService<TServiceInterface>() where TServiceInterface : class
        {
            var componentModel = GetRequiredMefService<IComponentModel, SComponentModel>();
            Assumes.Present(componentModel);
            return componentModel.GetService<TServiceInterface>(); ;
        }

        internal static object? GetService(IServiceProvider serviceProvider, Guid guidService, bool unique)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var guidInterface = VSConstants.IID_IUnknown;
            object? service = null;

            if (serviceProvider.QueryService(ref guidService, ref guidInterface, out var ptr) == 0 &&
                ptr != IntPtr.Zero)
            {
                try
                {
                    if (unique)
                    {
                        service = Marshal.GetUniqueObjectForIUnknown(ptr);
                    }
                    else
                    {
                        service = Marshal.GetObjectForIUnknown(ptr);
                    }
                }
                finally
                {
                    Marshal.Release(ptr);
                }
            }

            return service;
        }

        internal static IWpfTextView? GetWpfTextView(IVsWindowFrame vsWindowFrame)
        {
            IWpfTextView? wpfTextView = null;
            var vsTextView = VsShellUtilities.GetTextView(vsWindowFrame);

            if (vsTextView != null)
            {
                // TODO: Work out what dependency to bump, and use DefGuidList.guidIWpfTextViewHost
                var guidTextViewHost = new Guid("8C40265E-9FDB-4f54-A0FD-EBB72B7D0476");
                if (((IVsUserData)vsTextView).GetData(ref guidTextViewHost, out var textViewHost) == VSConstants.S_OK &&
                    textViewHost != null)
                {
                    wpfTextView = ((IWpfTextViewHost)textViewHost).TextView;
                }
            }

            return wpfTextView;
        }
    }
}
