// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(SolutionCloseTracker))]
    [System.Composition.Shared]
    internal class VisualStudioSolutionCloseTracker : SolutionCloseTracker, IVsSolutionEvents, IDisposable
    {
        private readonly IVsSolution? _solution;
        private uint _cookie;

        [ImportingConstructor]
        public VisualStudioSolutionCloseTracker(
           [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (serviceProvider.GetService(typeof(SVsSolution)) is IVsSolution solution)
            {
                _solution = solution;
                _solution.AdviseSolutionEvents(this, out _cookie);
            }
        }

        public void Dispose()
        {
            _solution?.UnadviseSolutionEvents(_cookie);
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            return HResult.NotImplemented;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return HResult.NotImplemented;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return HResult.NotImplemented;
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return HResult.NotImplemented;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return HResult.NotImplemented;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return HResult.NotImplemented;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            this.IsClosing = false;
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return HResult.NotImplemented;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            this.IsClosing = true;
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            this.IsClosing = false;
            return VSConstants.S_OK;
        }
    }
}
