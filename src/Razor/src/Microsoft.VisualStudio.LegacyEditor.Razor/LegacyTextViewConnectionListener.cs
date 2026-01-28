// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Razor;
using Microsoft.VisualStudio.Razor.Extensions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LegacyEditor.Razor;

[ContentType(RazorLanguage.CoreContentType)]
[ContentType(RazorConstants.LegacyCoreContentType)]
[TextViewRole(PredefinedTextViewRoles.Document)]
[Export(typeof(ITextViewConnectionListener))]
[method: ImportingConstructor]
internal sealed class LegacyTextViewConnectionListener(
    IRazorDocumentManager documentManager,
    JoinableTaskContext joinableTaskContext) : ITextViewConnectionListener
{
    private readonly IRazorDocumentManager _documentManager = documentManager;
    private readonly JoinableTaskContext _joinableTaskContext = joinableTaskContext;

    public void SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
    {
        _joinableTaskContext.AssertUIThread();

        _ = HandleAsync();

        async Task HandleAsync()
        {
            await _documentManager.OnTextViewOpenedAsync(textView, subjectBuffers);
        }
    }

    public void SubjectBuffersDisconnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
    {
        _ = HandleAsync();

        async Task HandleAsync()
        {
            await _documentManager.OnTextViewClosedAsync(textView, subjectBuffers);
        }
    }
}
