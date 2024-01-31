// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Editor.Razor;

[ContentType(RazorLanguage.CoreContentType)]
[ContentType(RazorConstants.LegacyCoreContentType)]
[TextViewRole(PredefinedTextViewRoles.Document)]
[Export(typeof(ITextViewConnectionListener))]
[method: ImportingConstructor]
internal class RazorTextViewConnectionListener(
    IRazorDocumentManager documentManager,
    JoinableTaskContext joinableTaskContext) : ITextViewConnectionListener
{
    private readonly IRazorDocumentManager _documentManager = documentManager;
    private readonly JoinableTaskContext _joinableTaskContext = joinableTaskContext;

    public void SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
    {
        _ = SubjectBuffersConnectedAsync(textView, subjectBuffers);
    }

    private async Task SubjectBuffersConnectedAsync(ITextView textView, IReadOnlyCollection<ITextBuffer> subjectBuffers)
    {
        try
        { 
            _joinableTaskContext.AssertUIThread();

            await _documentManager.OnTextViewOpenedAsync(textView, subjectBuffers);
        }
        catch (Exception ex)
        {
            Debug.Fail($"""
                RazorTextViewConnectionListener.SubjectBuffersConnected threw exception:
                {ex.Message}
                Stack trace:
                {ex.StackTrace}
                """);
        }
    }

    public void SubjectBuffersDisconnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
    {
        _ = SubjectBuffersDisconnectedAsync(textView, subjectBuffers);
    }

    public async Task SubjectBuffersDisconnectedAsync(ITextView textView, IReadOnlyCollection<ITextBuffer> subjectBuffers)
    {
        try
        {
            _joinableTaskContext.AssertUIThread();

            await _documentManager.OnTextViewClosedAsync(textView, subjectBuffers);
        }
        catch (Exception ex)
        {
            Debug.Fail($"""
                RazorTextViewConnectionListener.SubjectBuffersDisconnected threw exception:
                {ex.Message}
                Stack trace:
                {ex.StackTrace}
                """);
        }
    }
}
