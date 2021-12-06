// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;
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
        private readonly IVsTextManager4 _textManager;

        /// <summary>
        /// Protects concurrent modifications to _activeTextViews and _textBuffer's
        /// property bag.
        /// </summary>
        private readonly object _lock = new();

        #region protected by _lock
        private readonly List<ITextView> _activeTextViews = new();

        private ITextBuffer _textBuffer;
        #endregion

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
            _textManager = (IVsTextManager4)serviceProvider.GetService(typeof(SVsTextManager));

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

            if (!textView.TextBuffer.IsRazorLSPBuffer())
            {
                return;
            }

            lock (_lock)
            {
                _activeTextViews.Add(textView);

                // Initialize the user's options and start listening for changes.
                // We only want to attach the option changed event once so we don't receive multiple
                // notifications if there is more than one TextView active.
                if (!textView.TextBuffer.Properties.ContainsProperty(typeof(RazorEditorOptionsTracker)))
                {
                    // We assume there is ever only one TextBuffer at a time and thus all active
                    // TextViews have the same TextBuffer.
                    _textBuffer = textView.TextBuffer;

                    var bufferOptions = _editorOptionsFactory.GetOptions(_textBuffer);
                    var viewOptions = _editorOptionsFactory.GetOptions(textView);

                    Assumes.Present(bufferOptions);
                    Assumes.Present(viewOptions);

                    // All TextViews share the same options, so we only need to listen to changes for one.
                    // We need to keep track of and update both the TextView and TextBuffer options. Updating
                    // the TextView's options is necessary so 'SPC'/'TABS' in the bottom right corner of the
                    // view displays the right setting. Updating the TextBuffer is necessary since it's where
                    // LSP pulls settings from when sending us requests.
                    var optionsTracker = new RazorEditorOptionsTracker(TrackedView: textView, viewOptions, bufferOptions);
                    _textBuffer.Properties[typeof(RazorEditorOptionsTracker)] = optionsTracker;

                    // Initialize TextView options. We only need to do this once per TextView, as the options should
                    // automatically update and they aren't options we care about keeping track of.
                    InitializeRazorTextViewOptions(_textManager, optionsTracker);

                    // A change in Tools->Options settings only kicks off an options changed event in the view
                    // and not the buffer, i.e. even if we listened for TextBuffer option changes, we would never
                    // be notified. As a workaround, we listen purely for TextView changes, and update the
                    // TextBuffer options in the TextView listener as well.
                    RazorOptions_OptionChanged(null, null);
                    viewOptions.OptionChanged += RazorOptions_OptionChanged;
                }
            }
        }

        public void SubjectBuffersDisconnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
        {
            // When the TextView goes away so does the filter.  No need to do anything more.
            // However, we do need to detach from listening for option changes to avoid leaking.
            // We should switch to listening to a different TextView if the one we're listening
            // to is disconnected.
            Assumes.NotNull(_textBuffer);

            if (!textView.TextBuffer.IsRazorLSPBuffer())
            {
                return;
            }

            lock (_lock)
            {
                _activeTextViews.Remove(textView);

                // Is the tracked TextView where we listen for option changes the one being disconnected?
                // If so, see if another view is available.
                if (_textBuffer.Properties.TryGetProperty(
                    typeof(RazorEditorOptionsTracker), out RazorEditorOptionsTracker optionsTracker) &&
                    optionsTracker.TrackedView == textView)
                {
                    _textBuffer.Properties.RemoveProperty(typeof(RazorEditorOptionsTracker));
                    optionsTracker.ViewOptions.OptionChanged -= RazorOptions_OptionChanged;

                    // If there's another text view we can use to listen for options, start tracking it.
                    if (_activeTextViews.Count != 0)
                    {
                        var newTrackedView = _activeTextViews[0];
                        var newViewOptions = _editorOptionsFactory.GetOptions(newTrackedView);
                        Assumes.Present(newViewOptions);

                        // We assume the TextViews all have the same TextBuffer, so we can reuse the
                        // buffer options from the old TextView.
                        var newOptionsTracker = new RazorEditorOptionsTracker(
                            newTrackedView, newViewOptions, optionsTracker.BufferOptions);
                        _textBuffer.Properties[typeof(RazorEditorOptionsTracker)] = newOptionsTracker;

                        newViewOptions.OptionChanged += RazorOptions_OptionChanged;
                    }
                }
            }
        }

        private void RazorOptions_OptionChanged(object sender, EditorOptionChangedEventArgs e)
        {
            Assumes.NotNull(_textBuffer);

            if (!_textBuffer.Properties.TryGetProperty(typeof(RazorEditorOptionsTracker), out RazorEditorOptionsTracker optionsTracker))
            {
                return;
            }

            // Retrieve current space/tabs settings from from Tools->Options and update options in
            // the actual editor.
            var settings = UpdateRazorEditorOptions(_textManager, optionsTracker);

            // Keep track of accurate settings on the client side so we can easily retrieve the
            // options later when the server sends us a workspace/configuration request.
            _clientOptionsMonitor.UpdateOptions(settings);

            // Make sure the server updates the settings on their side by sending a
            // workspace/didChangeConfiguration request. This notifies the server that the user's
            // settings have changed. The server will then query the client's options monitor (already
            // updated via the line above) by sending a workspace/configuration request.
            // NOTE: This flow uses polyfilling because VS doesn't yet support workspace configuration
            // updates. Once they do, we can get rid of this extra logic.
            _ = _requestInvoker.ReinvokeRequestOnServerAsync<DidChangeConfigurationParams, Unit>(
                Methods.WorkspaceDidChangeConfigurationName,
                RazorLSPConstants.RazorLanguageServerName,
                CheckRazorServerCapability,
                new DidChangeConfigurationParams(),
                CancellationToken.None);
        }

        private static bool CheckRazorServerCapability(JToken token)
        {
            // We're talking cross-language servers here. Given the workspace/didChangeConfiguration is a normal LSP message this will only fail
            // if the Razor language server is not running. Typically this would be OK from a platform perspective; however VS will explode if
            // there's not a corresponding language server to accept the message. To protect ourselves from this scenario we can utilize capabilities
            // and just lookup generic Razor language server specific capabilities. If they exist we can succeed.
            var isRazorLanguageServer = RazorLanguageServerCapability.TryGet(token, out _);
            return isRazorLanguageServer;
        }

        private static void InitializeRazorTextViewOptions(IVsTextManager4 textManager, RazorEditorOptionsTracker optionsTracker)
        {
            var langPrefs3 = new LANGPREFERENCES3[] { new LANGPREFERENCES3() { guidLang = RazorLSPConstants.RazorLanguageServiceGuid } }; ;
            if (VSConstants.S_OK != textManager.GetUserPreferences4(null, langPrefs3, null))
            {
                return;
            }

            // General options
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.UseVirtualSpaceName, Convert.ToBoolean(langPrefs3[0].fVirtualSpace));

            var wordWrapStyle = WordWrapStyles.None;
            if (Convert.ToBoolean(langPrefs3[0].fWordWrap))
            {
                wordWrapStyle |= WordWrapStyles.WordWrap;
                if (Convert.ToBoolean(langPrefs3[0].fWordWrapGlyphs))
                {
                    wordWrapStyle |= WordWrapStyles.VisibleGlyphs;
                }
            }

            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.WordWrapStyleName, wordWrapStyle);
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.LineNumberMarginName, Convert.ToBoolean(langPrefs3[0].fLineNumbers));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.DisplayUrlsAsHyperlinksName, Convert.ToBoolean(langPrefs3[0].fHotURLs));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.BraceCompletionEnabledOptionName, Convert.ToBoolean(langPrefs3[0].fBraceCompletion));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewOptions.CutOrCopyBlankLineIfNoSelectionName, Convert.ToBoolean(langPrefs3[0].fCutCopyBlanks));

            // Scroll bar options
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.HorizontalScrollBarName, Convert.ToBoolean(langPrefs3[0].fShowHorizontalScrollBar));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.VerticalScrollBarName, Convert.ToBoolean(langPrefs3[0].fShowVerticalScrollBar));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowScrollBarAnnotationsOptionName, Convert.ToBoolean(langPrefs3[0].fShowAnnotations));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowChangeTrackingMarginOptionName, Convert.ToBoolean(langPrefs3[0].fShowChanges));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowMarksOptionName, Convert.ToBoolean(langPrefs3[0].fShowMarks));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowErrorsOptionName, Convert.ToBoolean(langPrefs3[0].fShowErrors));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowCaretPositionOptionName, Convert.ToBoolean(langPrefs3[0].fShowCaretPosition));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowEnhancedScrollBarOptionName, Convert.ToBoolean(langPrefs3[0].fUseMapMode));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.ShowPreviewOptionName, Convert.ToBoolean(langPrefs3[0].fShowPreview));
            optionsTracker.ViewOptions.SetOptionValue(DefaultTextViewHostOptions.PreviewSizeOptionName, (int)langPrefs3[0].uOverviewWidth);
        }

        private static EditorSettings UpdateRazorEditorOptions(IVsTextManager4 textManager, RazorEditorOptionsTracker optionsTracker)
        {
            var insertSpaces = RazorLSPOptions.Default.InsertSpaces;
            var tabSize = RazorLSPOptions.Default.TabSize;

            var langPrefs3 = new LANGPREFERENCES3[] { new LANGPREFERENCES3() { guidLang = RazorLSPConstants.RazorLanguageServiceGuid } }; ;
            if (VSConstants.S_OK != textManager.GetUserPreferences4(null, langPrefs3, null))
            {
                return new EditorSettings(indentWithTabs: !insertSpaces, tabSize);
            }

            // Tabs options
            insertSpaces = !Convert.ToBoolean(langPrefs3[0].fInsertTabs);
            tabSize = (int)langPrefs3[0].uTabSize;

            optionsTracker.ViewOptions.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, insertSpaces);
            optionsTracker.ViewOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, tabSize);

            // We need to update both the TextView and TextBuffer options for tabs/spaces settings. Updating the TextView
            // is necessary so 'SPC'/'TABS' in the bottom right corner of the view displays the right setting. Updating the
            // TextBuffer is necessary since it's where LSP pulls settings from when sending us requests.
            optionsTracker.BufferOptions.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, insertSpaces);
            optionsTracker.BufferOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, tabSize);

            return new EditorSettings(indentWithTabs: !insertSpaces, tabSize);
        }

        private class RazorLSPTextViewFilter : IOleCommandTarget, IVsTextViewFilter
        {
            private RazorLSPTextViewFilter()
            {
            }

            private IOleCommandTarget _next;

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
                pbstrText = null;
                return VSConstants.E_NOTIMPL;
            }

            public int GetPairExtents(int iLine, int iIndex, TextSpan[] pSpan) => VSConstants.E_NOTIMPL;
        }

        private record RazorEditorOptionsTracker(ITextView TrackedView, IEditorOptions ViewOptions, IEditorOptions BufferOptions);
    }
}
