// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudio.Razor;

internal sealed partial class RazorLSPTextViewConnectionListener
{
    private sealed class RazorLSPTextViewFilter : IOleCommandTarget, IVsTextViewFilter
    {
        private RazorLSPTextViewFilter()
        {
        }

        private IOleCommandTarget? _next;

        private IOleCommandTarget Next
        {
            get
            {
                if (_next is null)
                {
                    throw new InvalidOperationException($"{nameof(Next)} called before being set.");
                }

                return _next;
            }
            set
            {
                _next = value;
            }
        }

        public static void CreateAndRegister(IVsTextView textView)
        {
            var viewFilter = new RazorLSPTextViewFilter();
            textView.AddCommandFilter(viewFilter, out var next);

            viewFilter.Next = next;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            var queryResult = Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
            return queryResult;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            var execResult = Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            return execResult;
        }

        public int GetWordExtent(int iLine, int iIndex, uint dwFlags, TextSpan[] pSpan) => VSConstants.E_NOTIMPL;

        public int GetDataTipText(TextSpan[] pSpan, out string pbstrText)
        {
            pbstrText = null!;
            return VSConstants.E_NOTIMPL;
        }

        public int GetPairExtents(int iLine, int iIndex, TextSpan[] pSpan) => VSConstants.E_NOTIMPL;
    }
}
