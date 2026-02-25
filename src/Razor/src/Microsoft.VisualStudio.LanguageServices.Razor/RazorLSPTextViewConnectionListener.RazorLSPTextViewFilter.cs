// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor;

internal sealed partial class RazorLSPTextViewConnectionListener
{
    private sealed class RazorLSPTextViewFilter(
            ITextView textView,
            JoinableTaskFactory jtf,
            ImmutableArray<IInterceptedCommand> interceptedCommands) : IOleCommandTarget, IVsTextViewFilter
    {
        private readonly ITextView _textView = textView;
        private readonly JoinableTaskFactory _jtf = jtf;
        private readonly ImmutableArray<IInterceptedCommand> _interceptedCommands = interceptedCommands;

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

        public static void CreateAndRegister(
            IVsTextView vsTextView,
            ITextView textView,
            JoinableTaskFactory jtf,
            ImmutableArray<IInterceptedCommand> interceptedCommands)
        {
            var viewFilter = new RazorLSPTextViewFilter(textView, jtf, interceptedCommands);
            vsTextView.AddCommandFilter(viewFilter, out var next);

            viewFilter.Next = next;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (var command in _interceptedCommands)
            {
                var status = command.QueryStatus(pguidCmdGroup, prgCmds[0].cmdID);
                if (status != 0)
                {
                    prgCmds[0].cmdf = (uint)status;
                    return VSConstants.S_OK;
                }
            }

            return Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (var command in _interceptedCommands)
            {
                if (command.QueryStatus(pguidCmdGroup, nCmdID) == 0)
                {
                    continue;
                }

                _jtf.Run(() => ExecuteCommandAsync(nCmdID, command, CancellationToken.None));

                return VSConstants.S_OK;
            }

            return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private async Task ExecuteCommandAsync(uint nCmdID, IInterceptedCommand command, CancellationToken cancellationToken)
        {
            var textBuffer = _textView.TextBuffer;

            if (!textBuffer.TryGetTextDocument(out var razorDocument))
            {
                return;
            }

            var edits = await command.ExecuteAsync(
                razorDocument.Project.Solution,
                razorDocument.Id,
                nCmdID,
                cancellationToken).ConfigureAwait(false);

            if (edits.IsDefaultOrEmpty)
            {
                return;
            }

            await _jtf.SwitchToMainThreadAsync();

            using var edit = textBuffer.CreateEdit();
            foreach (var change in edits)
            {
                edit.Replace(change.Span.Start, change.Span.Length, change.NewText);
            }

            edit.Apply();
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
