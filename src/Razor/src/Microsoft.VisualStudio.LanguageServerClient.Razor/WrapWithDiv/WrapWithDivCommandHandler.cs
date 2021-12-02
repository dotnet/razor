// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Editor.Commanding;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.WrapWithDiv
{
    [Export(typeof(ICommandHandler))]
    [ContentType(RazorLSPConstants.RazorLSPContentTypeName)]
    [Name(nameof(WrapWithDivCommandHandler))]
    internal sealed class WrapWithDivCommandHandler : ICommandHandler<WrapWithDivCommandArgs>
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly JoinableTaskFactory _jtf;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;
        private readonly FormattingOptionsProvider _formattingOptionsProvider;

        public string DisplayName => nameof(WrapWithDivCommandHandler);

        [ImportingConstructor]
        public WrapWithDivCommandHandler(
            LSPRequestInvoker requestInvoker,
            JoinableTaskContext joinableTaskContext,
            ITextDocumentFactoryService textDocumentFactoryService,
            FormattingOptionsProvider formattingOptionsProvider)
        {
            _requestInvoker = requestInvoker;
            _jtf = joinableTaskContext.Factory;
            _textDocumentFactoryService = textDocumentFactoryService;
            _formattingOptionsProvider = formattingOptionsProvider;
        }
        public CommandState GetCommandState(WrapWithDivCommandArgs args)
        {
            if (!args.SubjectBuffer.IsRazorLSPBuffer())
            {
                return CommandState.Unavailable;
            }

            return CommandState.Available;
        }

        public bool ExecuteCommand(WrapWithDivCommandArgs args, CommandExecutionContext executionContext)
        {
            var buffer = args.SubjectBuffer;
            var snapshot = buffer.CurrentSnapshot;
            var viewSpanToWrap = args.TextView.Selection.StreamSelectionSpan.SnapshotSpan;

            // Handle projection view scenarios (eg: Tools.DiffFiles inline)
            var mappedSpansToWrap = args.TextView.BufferGraph.MapDownToBuffer(viewSpanToWrap, SpanTrackingMode.EdgeInclusive, buffer);

            if (mappedSpansToWrap.Count != 1)
            {
                // The selection spanned multiple buffers, not supported
                return false;
            }

            // Note: It's important to not use a jtf.Run here to try to achieve synchronous behavioe. JsonRPC doesn't carry over the
            //  synchronization context, which means unresponsiveness will occur if if the inproc json handler on the other side attempts to
            //  do a jtf.SwitchToMainThread.
            // Also, intentionally not using IUIThreadOperationExecutor to bring up the wait dialog as there would be interweaved
            //  creation/disposal of these objects since vs's commanding infrastructure creates/disposes an instance around their
            //  call into this handler.
            _ = _jtf.RunAsync(async () =>
            {
                var result = await InvokeWrapWithDivAsync(snapshot, mappedSpansToWrap[0], buffer);

                if (result is not null && result.TextEdits.Length > 0)
                {
                    ApplyChanges(snapshot, result, args.TextView);
                }
            });

            return true;
        }

        private async Task<VSInternalWrapWithTagResponse?> InvokeWrapWithDivAsync(ITextSnapshot snapshot, SnapshotSpan spanToWrap, ITextBuffer subjectBuffer)
        {
            if (!_textDocumentFactoryService.TryGetTextDocument(subjectBuffer, out var document) ||
                document?.FilePath is null)
            {
                return null;
            }

            var hostUri = new Uri(document.FilePath);

            var wrapWithTagParams = new VSInternalWrapWithTagParams(
                range: spanToWrap.AsRange(),
                tagName: "div",
                options: _formattingOptionsProvider.GetOptions(hostUri),
                textDocument: new TextDocumentIdentifier() { Uri = hostUri }
            );

            // Send the WrapWithTag request to the appropriate language server.
            var wrapWithTagResponse = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalWrapWithTagParams, VSInternalWrapWithTagResponse>(
                    subjectBuffer,
                    LanguageServerConstants.RazorWrapWithTagEndpoint,
                    RazorLSPConstants.RazorLanguageServerName,
                    wrapWithTagParams,
                    CancellationToken.None).ConfigureAwait(false);

            return wrapWithTagResponse?.Response;
        }

        private static void ApplyChanges(ITextSnapshot snapshot, VSInternalWrapWithTagResponse wrapWithTagResponse, ITextView? textView = null)
        {
            // Make sure we're still looking at the same snapshot before we try to change anything
            if (snapshot != snapshot.TextBuffer.CurrentSnapshot)
            {
                return;

            }

            var buffer = snapshot.TextBuffer;
            using var edit = buffer.CreateEdit();
            foreach (var curEdit in wrapWithTagResponse.TextEdits)
            {
                if (ToSnapshotSpan(curEdit.Range, snapshot) is SnapshotSpan currentSpan)
                {
                    edit.Replace(currentSpan.Start, currentSpan.Length, curEdit.NewText);
                }
            }

            var newSnapshot = edit.Apply();

            if (textView is not null && ToSnapshotSpan(wrapWithTagResponse.TagRange, snapshot) is SnapshotSpan newSelection)
            {
                var bufferNewSelection = new SnapshotSpan(newSnapshot, newSelection);
                var newSelectionsInView = textView.BufferGraph.MapUpToBuffer(bufferNewSelection, SpanTrackingMode.EdgeInclusive, textView.TextBuffer);

                if (newSelectionsInView.Count == 1)
                {
                    textView.Selection.Select(newSelectionsInView[0], true);
                    textView.Caret.MoveTo(textView.Selection.Start);
                }
            }
        }

        private static int? GetPositionFromLineColumn(ITextSnapshot snapshot, int line, int column)
        {
            if ((line >= 0) && (line < snapshot.LineCount))
            {
                var textLine = snapshot.GetLineFromLineNumber(line);

                // Non-strict equality below, because caret can be position *after*
                // the last character of the line. So for line of length 1 both
                // column 0 and column 1 are valid caret locations.
                if (column <= textLine.Length)
                {
                    return textLine.Start + column;
                }
            }

            return null;
        }

        private static SnapshotSpan? ToSnapshotSpan(Range range, ITextSnapshot snapshot)
        {
            if (ToSnapshotPoint(range.Start, snapshot) is SnapshotPoint startPoint &&
                ToSnapshotPoint(range.End, snapshot) is SnapshotPoint endPoint)
            {
                return new SnapshotSpan(startPoint, endPoint);
            }

            return null;
        }

        private static SnapshotPoint? ToSnapshotPoint(Position position, ITextSnapshot snapshot)
        {
            if (GetPositionFromLineColumn(snapshot, position.Line, position.Character) is int positionIndex)
            {
                return new SnapshotPoint(snapshot, positionIndex);
            }

            return null;
        }
    }

#nullable disable

    internal class WrapWithDivCommandBindings
    {
        // These values match those in RazorContextMenu.vsct
        internal const string GuidRazorGroupString = "a3a603a2-2b17-4ce2-bd21-cbb8ccc084ec";
        internal const uint CmdIDWrapWithDiv = 0x101;

        // This exports this command handler to handle the menu item defined in the vsct file
        [Export]
        [CommandBinding(GuidRazorGroupString, CmdIDWrapWithDiv, typeof(WrapWithDivCommandArgs))]
        internal CommandBindingDefinition wrapWithDivCommandBinding;
    }

    // EditorCommandArgs is abstract, so we need to subclass it, but we have no value to add
    internal class WrapWithDivCommandArgs : EditorCommandArgs
    {
        public WrapWithDivCommandArgs(ITextView textView, ITextBuffer subjectBuffer)
            : base(textView, subjectBuffer)
        {
        }
    }
}
