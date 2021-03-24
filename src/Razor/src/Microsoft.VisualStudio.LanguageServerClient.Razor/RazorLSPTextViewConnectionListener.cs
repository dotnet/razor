// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using DidChangeConfigurationParams = OmniSharp.Extensions.LanguageServer.Protocol.Models.DidChangeConfigurationParams;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    /// <summary>
    /// The entire purpose of this class is to enable us to apply our TextView filter to Razor text views in order to work around lacking debugging support in the
    /// LSP platform for default language servers. Ultimately this enables us to provide "hover" results 
    /// </summary>
    [Export(typeof(ITextViewConnectionListener))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [ContentType(RazorLSPConstants.RazorLSPContentTypeName)]
    internal class RazorLSPTextViewConnectionListener : ITextViewConnectionListener
    {
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactory;
        private readonly LSPEditorFeatureDetector _editorFeatureDetector;
        private readonly IEditorOptionsFactoryService _editorOptionsFactory;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly RazorLSPClientOptionsMonitor _clientOptionsMonitor;
        private readonly IVsTextManager2 _textManager;

        private IEditorOptions _razorEditorOptions;

        [ImportingConstructor]
        public RazorLSPTextViewConnectionListener(
            IVsEditorAdaptersFactoryService editorAdaptersFactory,
            LSPEditorFeatureDetector editorFeatureDetector,
            IEditorOptionsFactoryService editorOptionsFactory,
            LSPRequestInvoker requestInvoker,
            RazorLSPClientOptionsMonitor clientOptionsMonitor,
            SVsServiceProvider serviceProvider)
        {
            if (editorAdaptersFactory is null)
            {
                throw new ArgumentNullException(nameof(editorAdaptersFactory));
            }

            if (editorFeatureDetector is null)
            {
                throw new ArgumentNullException(nameof(editorFeatureDetector));
            }

            if (editorOptionsFactory is null)
            {
                throw new ArgumentNullException(nameof(editorOptionsFactory));
            }

            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            if (clientOptionsMonitor is null)
            {
                throw new ArgumentNullException(nameof(clientOptionsMonitor));
            }

            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _editorAdaptersFactory = editorAdaptersFactory;
            _editorFeatureDetector = editorFeatureDetector;
            _editorOptionsFactory = editorOptionsFactory;
            _requestInvoker = requestInvoker;
            _clientOptionsMonitor = clientOptionsMonitor;
            _textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;

            Assumes.Present(_textManager);
        }

        public void SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
        {
            if (textView is null)
            {
                throw new ArgumentNullException(nameof(textView));
            }

            var vsTextView = _editorAdaptersFactory.GetViewAdapter(textView);

            // In remote client scenarios there's a custom language service applied to buffers in order to enable delegation of interactions.
            // Because of this we don't want to break that experience so we ensure not to "set" a langauge service for remote clients.
            if (!_editorFeatureDetector.IsRemoteClient())
            {
                vsTextView.GetBuffer(out var vsBuffer);
                vsBuffer.SetLanguageServiceID(RazorLSPConstants.RazorLanguageServiceGuid);
            }

            RazorLSPTextViewFilter.CreateAndRegister(vsTextView);

            // Initialize the user's options and start listening for changes.
            _razorEditorOptions = _editorOptionsFactory.GetOptions(textView);
            Assumes.Present(_razorEditorOptions);
            RazorOptions_OptionChanged(null, null);
            _razorEditorOptions.OptionChanged += RazorOptions_OptionChanged;
        }

        public void SubjectBuffersDisconnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
        {
            // When the TextView goes away so does the filter.  No need to do anything more.
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void RazorOptions_OptionChanged(object sender, EditorOptionChangedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            // Retrieve current space/tabs settings from from Tools->Options.
            var (insertSpaces, tabSize) = GetRazorEditorOptions(_textManager);

            // Update settings in the actual editor.
            _razorEditorOptions.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, insertSpaces);
            _razorEditorOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, tabSize);

            // Keep track of accurate settings on the client side so we can easily retrieve the
            // options later when the server sends us a workspace/configuration request.
            _clientOptionsMonitor.UpdateOptions(insertSpaces, tabSize);

            // Make sure the server updates the settings on their side by sending a
            // workspace/didChangeConfiguration request. This notifies the server that the user's
            // settings have changed. The server will then query the client's options monitor (already
            // updated via the line above) by sending a workspace/configuration request. 
            await _requestInvoker.ReinvokeRequestOnServerAsync<DidChangeConfigurationParams, Unit>(
                Methods.WorkspaceDidChangeConfigurationName,
                RazorLSPConstants.RazorLSPContentTypeName,
                new DidChangeConfigurationParams(),
                CancellationToken.None);
        }

        private static (bool insertSpaces, int tabSize) GetRazorEditorOptions(IVsTextManager2 textManager)
        {
            var insertSpaces = RazorLSPOptions.Default.InsertSpaces;
            var tabSize = RazorLSPOptions.Default.TabSize;

            var langPrefs2 = new LANGPREFERENCES2[] { new LANGPREFERENCES2() { guidLang = RazorLSPConstants.RazorLanguageServiceGuid } };
            if (VSConstants.S_OK == textManager.GetUserPreferences2(null, null, langPrefs2, null))
            {
                insertSpaces = langPrefs2[0].fInsertTabs == 0;
                tabSize = (int)langPrefs2[0].uTabSize;
            }

            return (insertSpaces, tabSize);
        }

        private class RazorLSPTextViewFilter : IOleCommandTarget, IVsTextViewFilter
        {
            private RazorLSPTextViewFilter()
            {
            }

            private IOleCommandTarget Next { get; set; }

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
                pbstrText = null;
                return VSConstants.E_NOTIMPL;
            }

            public int GetPairExtents(int iLine, int iIndex, TextSpan[] pSpan) => VSConstants.E_NOTIMPL;
        }
    }
}
