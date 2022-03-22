// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [ContentType(RazorLanguage.CoreContentType)]
    [ContentType(RazorConstants.LegacyCoreContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Export(typeof(ITextViewConnectionListener))]
    internal class RazorTextViewConnectionListener : ITextViewConnectionListener
    {
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly RazorDocumentManager _documentManager;

        [ImportingConstructor]
        public RazorTextViewConnectionListener(JoinableTaskContext joinableTaskContext!!, RazorDocumentManager documentManager!!)
        {
            _joinableTaskContext = joinableTaskContext;
            _documentManager = documentManager;
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        public async void SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                if (textView is null)
                {
                    throw new ArgumentException(nameof(textView));
                }

                if (subjectBuffers is null)
                {
                    throw new ArgumentNullException(nameof(subjectBuffers));
                }

                _joinableTaskContext.AssertUIThread();
                await _documentManager.OnTextViewOpenedAsync(textView, subjectBuffers);
            }
            catch (Exception ex)
            {
                Debug.Fail("RazorTextViewConnectionListener.SubjectBuffersConnected threw exception:" +
                    Environment.NewLine + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace);
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        public async void SubjectBuffersDisconnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                if (textView is null)
                {
                    throw new ArgumentException(nameof(textView));
                }

                if (subjectBuffers is null)
                {
                    throw new ArgumentNullException(nameof(subjectBuffers));
                }

                _joinableTaskContext.AssertUIThread();
                await _documentManager.OnTextViewClosedAsync(textView, subjectBuffers);
            }
            catch (Exception ex)
            {
                Debug.Fail("RazorTextViewConnectionListener.SubjectBuffersDisconnected threw exception:" +
                    Environment.NewLine + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace);
            }
        }
    }
}
