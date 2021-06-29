// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [ContentType(RazorLanguage.CoreContentType)]
    [ContentType(RazorConstants.LegacyCoreContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [Export(typeof(ITextViewConnectionListener))]
    internal class RazorTextViewConnectionListener : ITextViewConnectionListener
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly RazorDocumentManager _documentManager;

        [ImportingConstructor]
        public RazorTextViewConnectionListener(ForegroundDispatcher foregroundDispatcher, RazorDocumentManager documentManager)
        {
            if (foregroundDispatcher == null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentManager == null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentManager = documentManager;
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        public async void SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                if (textView == null)
                {
                    throw new ArgumentException(nameof(textView));
                }

                if (subjectBuffers == null)
                {
                    throw new ArgumentNullException(nameof(subjectBuffers));
                }

                await _foregroundDispatcher.RunOnForegroundAsync(
                    () => _documentManager.OnTextViewOpened(textView, subjectBuffers), CancellationToken.None);
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
                if (textView == null)
                {
                    throw new ArgumentException(nameof(textView));
                }

                if (subjectBuffers == null)
                {
                    throw new ArgumentNullException(nameof(subjectBuffers));
                }

                await _foregroundDispatcher.RunOnForegroundAsync(
                    () => _documentManager.OnTextViewClosed(textView, subjectBuffers), CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.Fail("RazorTextViewConnectionListener.SubjectBuffersDisconnected threw exception:" +
                    Environment.NewLine + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace);
            }
        }
    }
}
