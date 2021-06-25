// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.Editor.Razor.Documents
{
    internal class RunningDocumentTableEventSink : IVsRunningDocTableEvents3
    {
        private readonly VisualStudioEditorDocumentManager _documentManager;
        private readonly ForegroundDispatcher _foregroundDispatcher;

        public RunningDocumentTableEventSink(VisualStudioEditorDocumentManager documentManager, ForegroundDispatcher foregroundDispatcher)
        {
            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            _documentManager = documentManager;
            _foregroundDispatcher = foregroundDispatcher;
        }

        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            _ = Task.Factory.StartNew(() => OnAfterAttributeChangeExAsync(
                docCookie, grfAttribs, pszMkDocumentOld, pszMkDocumentNew),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);
            return VSConstants.S_OK;
        }

        private async Task OnAfterAttributeChangeExAsync(uint docCookie, uint grfAttribs, string pszMkDocumentOld, string pszMkDocumentNew)
        {
            await _foregroundDispatcher.RunOnForegroundAsync(async () =>
            {
                // Document has been initialized.
                if ((grfAttribs & (uint)__VSRDTATTRIB3.RDTA_DocumentInitialized) != 0)
                {
                    await _documentManager.DocumentOpenedAsync(docCookie);
                }

                if ((grfAttribs & (uint)__VSRDTATTRIB.RDTA_MkDocument) != 0)
                {
                    await _documentManager.DocumentRenamedAsync(docCookie, pszMkDocumentOld, pszMkDocumentNew);
                }
            }, CancellationToken.None);
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            _ = Task.Factory.StartNew(() => OnBeforeLastDocumentUnlockAsync(
                docCookie, dwReadLocksRemaining, dwEditLocksRemaining),
                CancellationToken.None,
                TaskCreationOptions.None,
                TaskScheduler.Default);
            return VSConstants.S_OK;
        }

        private async Task OnBeforeLastDocumentUnlockAsync(uint docCookie, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            await _foregroundDispatcher.RunOnForegroundAsync(() =>
            {
                // Document is being closed
                if (dwReadLocksRemaining + dwEditLocksRemaining == 0)
                {
                    _documentManager.DocumentClosed(docCookie);
                }
            }, CancellationToken.None);
        }

        public int OnBeforeSave(uint docCookie) => VSConstants.S_OK;

        public int OnAfterSave(uint docCookie) => VSConstants.S_OK;

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) => VSConstants.S_OK;

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;
    }
}
