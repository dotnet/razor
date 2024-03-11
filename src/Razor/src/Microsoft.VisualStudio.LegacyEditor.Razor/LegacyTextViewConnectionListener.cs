// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Shell;
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
    [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
    IRazorDocumentManager documentManager,
    JoinableTaskContext joinableTaskContext) : ITextViewConnectionListener
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IRazorDocumentManager _documentManager = documentManager;
    private readonly JoinableTaskContext _joinableTaskContext = joinableTaskContext;

    private RazorStartupInitializer? _startupInitializer;

    public void SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
    {
        _joinableTaskContext.AssertUIThread();

        InitializeStartupServices();

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

    /// <summary>
    ///  Ensures that Razor startup services are instantiated and running.
    /// </summary>
    private void InitializeStartupServices()
    {
        if (_startupInitializer is null)
        {
            Interlocked.CompareExchange(ref _startupInitializer, GetStartupInitializer(_serviceProvider), null);
        }

        static RazorStartupInitializer GetStartupInitializer(IServiceProvider serviceProvider)
        {
            var componentModel = serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            Assumes.Present(componentModel);
            return componentModel.DefaultExportProvider.GetExportedValue<RazorStartupInitializer>();
        }
    }
}
