// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Shared]
    [Export(typeof(RazorLanguageClientMiddleLayer))]
    internal class DefaultRazorLanguageClientMiddleLayer : RazorLanguageClientMiddleLayer
    {
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly LSPDocumentManager _documentManager;
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public DefaultRazorLanguageClientMiddleLayer(
            JoinableTaskContext joinableTaskContext,
            LSPDocumentManager documentManager,
            SVsServiceProvider serviceProvider)
        {
            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _joinableTaskFactory = joinableTaskContext.Factory;
            _documentManager = documentManager;
            _serviceProvider = serviceProvider;
        }

        public override bool CanHandle(string methodName)
        {
            return methodName == Methods.TextDocumentOnTypeFormattingName;
        }

        public override Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
        {
            return null;
        }

        public override async Task<JToken> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken>> sendRequest)
        {
            if (methodName == Methods.TextDocumentOnTypeFormattingName)
            {
                var emptyResult = JToken.FromObject(Array.Empty<TextEdit>());
                var requestParams = methodParam.ToObject<DocumentOnTypeFormattingParams>();
                if (requestParams.Options.OtherOptions == null)
                {
                    requestParams.Options.OtherOptions = new Dictionary<string, object>();
                }

                requestParams.Options.OtherOptions[LanguageServerConstants.ExpectsCursorPlaceholderKey] = true;
                var token = JToken.FromObject(requestParams);
                var result = await sendRequest(token);
                var edits = result?.ToObject<TextEdit[]>();
                if (edits == null)
                {
                    return emptyResult;
                }

                await _joinableTaskFactory.SwitchToMainThreadAsync();

                if (!_documentManager.TryGetDocument(requestParams.TextDocument.Uri, out var documentSnapshot))
                {
                    return emptyResult;
                }

                Position cursorPosition = null;
                var filteredEdits = new List<TextEdit>(edits.Length);
                foreach (var edit in edits)
                {
                    if (edit.NewText.Contains(LanguageServerConstants.CursorPlaceholderString))
                    {
                        var newEdit = new TextEdit();
                        newEdit.Range = edit.Range;
                        newEdit.NewText = edit.NewText.Replace(LanguageServerConstants.CursorPlaceholderString, string.Empty);
                        filteredEdits.Add(newEdit);

                        cursorPosition = edit.Range.Start;
                    }
                    else
                    {
                        filteredEdits.Add(edit);
                    }
                }

                ApplyTextEdits(filteredEdits, documentSnapshot.Snapshot, documentSnapshot.Snapshot.TextBuffer);

                if (cursorPosition != null)
                {
                    var fullPath = GetLocalFilePath(requestParams.TextDocument.Uri);
                    VsShellUtilities.OpenDocument(_serviceProvider, fullPath, VSConstants.LOGVIEWID.TextView_guid, out _, out _, out _, out var textView);
                    MoveCaretToPosition(textView, cursorPosition);
                }

                return emptyResult;
            }
            else
            {
                return await sendRequest(methodParam);
            }
        }

        internal static void ApplyTextEdits(IEnumerable<TextEdit> textEdits, ITextSnapshot snapshot, ITextBuffer textBuffer)
        {
            var vsTextEdit = textBuffer.CreateEdit();
            foreach (var textEdit in textEdits)
            {
                if (textEdit.Range.Start == textEdit.Range.End)
                {
                    var position = GetSnapshotPositionFromProtocolPosition(snapshot, textEdit.Range.Start);
                    if (position > -1)
                    {
                        var span = GetTranslatedSpan(position, 0, snapshot, vsTextEdit.Snapshot);
                        vsTextEdit.Insert(span.Start, textEdit.NewText);
                    }
                }
                else if (string.IsNullOrEmpty(textEdit.NewText))
                {
                    var startPosition = GetSnapshotPositionFromProtocolPosition(snapshot, textEdit.Range.Start);
                    var endPosition = GetSnapshotPositionFromProtocolPosition(snapshot, textEdit.Range.End);
                    var difference = endPosition - startPosition;
                    if (startPosition > -1 && endPosition > -1 && difference > 0)
                    {
                        var span = GetTranslatedSpan(startPosition, difference, snapshot, vsTextEdit.Snapshot);
                        vsTextEdit.Delete(span);
                    }
                }
                else
                {
                    var startPosition = GetSnapshotPositionFromProtocolPosition(snapshot, textEdit.Range.Start);
                    var endPosition = GetSnapshotPositionFromProtocolPosition(snapshot, textEdit.Range.End);
                    var difference = endPosition - startPosition;

                    if (startPosition > -1 && endPosition > -1 && difference > 0)
                    {
                        var span = GetTranslatedSpan(startPosition, difference, snapshot, vsTextEdit.Snapshot);
                        vsTextEdit.Replace(span, textEdit.NewText);
                    }
                }
            }

            vsTextEdit.Apply();
        }

        private static Span GetTranslatedSpan(int startPosition, int length, ITextSnapshot oldSnapshot, ITextSnapshot newSnapshot)
        {
            var span = new Span(startPosition, length);

            if (oldSnapshot.Version != newSnapshot.Version)
            {
                var snapshotSpan = new SnapshotSpan(oldSnapshot, span);
                var translatedSnapshotSpan = snapshotSpan.TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive);
                span = translatedSnapshotSpan.Span;
            }

            return span;
        }

        internal static SnapshotPoint GetSnapshotPositionFromProtocolPosition(ITextSnapshot textSnapshot, Position position)
        {
            var line = textSnapshot.GetLineFromLineNumber(position.Line);
            var snapshotPosition = line.Start + position.Character;

            return new SnapshotPoint(textSnapshot, snapshotPosition);
        }

        internal static void MoveCaretToPosition(IVsTextView textView, Position position, bool sendFocus = true)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            textView.SetCaretPos(position.Line, position.Character);
            textView.EnsureSpanVisible(new TextSpan() { iStartIndex = position.Character, iStartLine = position.Line, iEndIndex = position.Character, iEndLine = position.Line });
            textView.CenterLines(position.Line, 1);
            if (sendFocus)
            {
                textView.SendExplicitFocus();
            }
        }

        internal static string GetLocalFilePath(Uri documentUri)
        {
            Requires.Argument(documentUri.IsFile, nameof(documentUri), "There were no clients that can open the document.");

            // Note: this would remove the '/' from some Uri returned on some LSP providers
            var absolutePath = documentUri.LocalPath.TrimStart('/');
            var fullPath = Path.GetFullPath(absolutePath);

            return fullPath;
        }
    }
}
