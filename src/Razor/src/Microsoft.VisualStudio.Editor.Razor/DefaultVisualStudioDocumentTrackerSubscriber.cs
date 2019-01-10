// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [System.Composition.Shared]
    [Export(typeof(VisualStudioDocumentTrackerSubscriber))]
    internal class DefaultVisualStudioDocumentTrackerSubscriber : VisualStudioDocumentTrackerSubscriber
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly IBufferGraphFactoryService _bufferGraphService;
        private readonly WorkspaceStateFactory _workspaceStateFactory;

        [ImportingConstructor]
        public DefaultVisualStudioDocumentTrackerSubscriber(
            ForegroundDispatcher foregroundDispatcher,
            IBufferGraphFactoryService bufferGraphService,
            WorkspaceStateFactory workspaceStateFactory)
        {
            if (foregroundDispatcher == null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (bufferGraphService == null)
            {
                throw new ArgumentNullException(nameof(bufferGraphService));
            }

            if (workspaceStateFactory == null)
            {
                throw new ArgumentNullException(nameof(workspaceStateFactory));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _bufferGraphService = bufferGraphService;
            _workspaceStateFactory = workspaceStateFactory;
        }

        public override void Unsubscribe(DefaultVisualStudioDocumentTracker documentTracker)
        {
            if (documentTracker == null)
            {
                throw new ArgumentNullException(nameof(documentTracker));
            }

            _foregroundDispatcher.AssertForegroundThread();

            var diskBuffer = documentTracker.TextBuffer;
            if (diskBuffer.Properties.TryGetProperty<CSharpBufferChangeListener>(typeof(CSharpBufferChangeListener), out var csharpBufferChangeListener))
            {
                diskBuffer.Properties.RemoveProperty(typeof(CSharpBufferChangeListener));
                csharpBufferChangeListener.Dispose();
            }

            documentTracker.Unsubscribe();
        }

        public override void Subscribe(DefaultVisualStudioDocumentTracker documentTracker)
        {
            if (documentTracker == null)
            {
                throw new ArgumentNullException(nameof(documentTracker));
            }

            _foregroundDispatcher.AssertForegroundThread();

            if (documentTracker.TextViews.Count == 0)
            {
                return;
            }

            var diskBuffer = documentTracker.TextBuffer;
            if (!diskBuffer.Properties.TryGetProperty<CSharpBufferChangeListener>(typeof(CSharpBufferChangeListener), out var csharpBufferChangeListener))
            {
                var changeListener = new CSharpBufferChangeListener(documentTracker, OnCSharpBuffersChange);
                diskBuffer.Properties.AddProperty(typeof(CSharpBufferChangeListener), changeListener);

                var viewBufferGraph = documentTracker.TextViews[0].BufferGraph;
                changeListener.ListenForChanges(viewBufferGraph);
            }
        }

        private void OnCSharpBuffersChange(DefaultVisualStudioDocumentTracker documentTracker, IReadOnlyCollection<ITextBuffer> candidateBuffers)
        {
            var diskBuffer = documentTracker.TextBuffer;
            if (!TrySubscribeDocumentTracker(documentTracker, candidateBuffers))
            {
                return;
            }

            if (diskBuffer.Properties.TryGetProperty<CSharpBufferChangeListener>(typeof(CSharpBufferChangeListener), out var storedChangeListener))
            {
                diskBuffer.Properties.RemoveProperty(typeof(CSharpBufferChangeListener));
                storedChangeListener.Dispose();
            }
        }

        private bool TrySubscribeDocumentTracker(DefaultVisualStudioDocumentTracker documentTracker, IReadOnlyCollection<ITextBuffer> candidateBuffers)
        {
            _foregroundDispatcher.AssertForegroundThread();

            foreach (var candidateBuffer in candidateBuffers)
            {
                var workspace = candidateBuffer.GetWorkspace();
                if (workspace == null)
                {
                    continue;
                }

                var workspaceState = _workspaceStateFactory.Create(workspace);
                documentTracker.Subscribe(workspaceState);
                return true;
            }

            return false;
        }

        private class CSharpBufferChangeListener : IDisposable
        {
            private const string CSharpContentType = "CSharp";
            private const string LiveShareCSharpContentType = "C#_LSP";
            private readonly DefaultVisualStudioDocumentTracker _documentTracker;
            private readonly Action<DefaultVisualStudioDocumentTracker, IReadOnlyCollection<ITextBuffer>> _onCSharpBuffersChanged;
            private readonly List<ITextBuffer> _csharpBuffers;
            private BufferGraphChangeListener _bufferGraphChangeListener;

            public CSharpBufferChangeListener(
                DefaultVisualStudioDocumentTracker documentTracker,
                Action<DefaultVisualStudioDocumentTracker, IReadOnlyCollection<ITextBuffer>> onCSharpBuffersChanged)
            {
                _csharpBuffers = new List<ITextBuffer>();
                _documentTracker = documentTracker;
                _onCSharpBuffersChanged = onCSharpBuffersChanged;

            }

            public void ListenForChanges(IBufferGraph bufferGraph)
            {
                if (_bufferGraphChangeListener != null)
                {
                    throw new InvalidOperationException(nameof(ListenForChanges) + " should only be called once per instance of " + nameof(CSharpBufferChangeListener));
                }

                var csharpBuffers = bufferGraph.GetTextBuffers(IsCSharpBuffer);
                foreach (var csharpBuffer in _csharpBuffers)
                {
                    TryAddCSharpBuffer(csharpBuffer);
                }
                _bufferGraphChangeListener = new BufferGraphChangeListener(bufferGraph, OnBufferGraphChange);

                _onCSharpBuffersChanged(_documentTracker, _csharpBuffers);
            }

            private void TryAddCSharpBuffer(ITextBuffer textBuffer)
            {
                if (!IsCSharpBuffer(textBuffer))
                {
                    return;
                }

                AttachWorkspaceChangeListener(textBuffer, OnWorkspaceChangedForBuffer);

                _csharpBuffers.Add(textBuffer);
            }

            private void TryRemoveCSharpBuffer(ITextBuffer textBuffer)
            {
                if (textBuffer.Properties.TryGetProperty<WorkspaceChangeListener>(typeof(WorkspaceChangeListener), out var changeListener))
                {
                    textBuffer.Properties.RemoveProperty(typeof(WorkspaceChangeListener));
                    changeListener.Dispose();
                    _csharpBuffers.Remove(textBuffer);
                }
            }

            private void OnBufferGraphChange(GraphBuffersChangedEventArgs args)
            {
                foreach (var addedBuffer in args.AddedBuffers)
                {
                    TryAddCSharpBuffer(addedBuffer);
                }

                foreach (var removedBuffer in args.RemovedBuffers)
                {
                    TryRemoveCSharpBuffer(removedBuffer);
                }

                _onCSharpBuffersChanged(_documentTracker, _csharpBuffers);
            }

            private void OnWorkspaceChangedForBuffer(ITextBuffer textBuffer)
            {
                if (!IsCSharpBuffer(textBuffer))
                {
                    return;
                }

                _onCSharpBuffersChanged(_documentTracker, _csharpBuffers);
            }

            private void AttachWorkspaceChangeListener(
                ITextBuffer textBuffer,
                Action<ITextBuffer> onWorkspaceChanged)
            {
                Debug.Assert(!textBuffer.Properties.ContainsProperty(typeof(WorkspaceChangeListener)));

                var workspaceChangeListener = new WorkspaceChangeListener(textBuffer, onWorkspaceChanged);
                textBuffer.Properties[typeof(WorkspaceChangeListener)] = workspaceChangeListener;
            }

            public void Dispose()
            {
                _bufferGraphChangeListener.Dispose();

                for (var i = _csharpBuffers.Count - 1; i >= 0; i--)
                {
                    TryRemoveCSharpBuffer(_csharpBuffers[i]);
                }
            }

            private static bool IsCSharpBuffer(ITextBuffer textBuffer) =>
                textBuffer.ContentType.IsOfType(CSharpContentType) || textBuffer.ContentType.IsOfType(LiveShareCSharpContentType);
        }

        private class WorkspaceChangeListener : IDisposable
        {
            private readonly Action<ITextBuffer> _workspaceChangedForBuffer;
            private readonly ITextBuffer _candidateBuffer;
            private readonly WorkspaceRegistration _workspaceRegistration;

            public WorkspaceChangeListener(
                ITextBuffer candidateBuffer,
                Action<ITextBuffer> workspaceChangedForBuffer)
            {
                if (candidateBuffer == null)
                {
                    throw new ArgumentNullException(nameof(candidateBuffer));
                }

                if (workspaceChangedForBuffer == null)
                {
                    throw new ArgumentNullException(nameof(workspaceChangedForBuffer));
                }

                _workspaceChangedForBuffer = workspaceChangedForBuffer;
                _candidateBuffer = candidateBuffer;

                var textContainer = _candidateBuffer.AsTextContainer();
                _workspaceRegistration = Workspace.GetWorkspaceRegistration(textContainer);
                _workspaceRegistration.WorkspaceChanged += WorkspaceRegistration_WorkspaceChanged;
            }

            private void WorkspaceRegistration_WorkspaceChanged(object sender, EventArgs args) => _workspaceChangedForBuffer(_candidateBuffer);

            public void Dispose()
            {
                _workspaceRegistration.WorkspaceChanged -= WorkspaceRegistration_WorkspaceChanged;
            }
        }

        private class BufferGraphChangeListener : IDisposable
        {
            private readonly Action<GraphBuffersChangedEventArgs> _graphBuffersChanged;
            private readonly IBufferGraph _bufferGraph;

            public BufferGraphChangeListener(
                IBufferGraph bufferGraph,
                Action<GraphBuffersChangedEventArgs> graphBuffersChanged)
            {
                if (bufferGraph == null)
                {
                    throw new ArgumentNullException(nameof(bufferGraph));
                }

                if (graphBuffersChanged == null)
                {
                    throw new ArgumentNullException(nameof(graphBuffersChanged));
                }

                _bufferGraph = bufferGraph;
                _graphBuffersChanged = graphBuffersChanged;
                _bufferGraph.GraphBuffersChanged += BufferGraph_GraphBuffersChanged;
            }

            private void BufferGraph_GraphBuffersChanged(object sender, GraphBuffersChangedEventArgs args) => _graphBuffersChanged(args);

            public void Dispose()
            {
                _bufferGraph.GraphBuffersChanged -= BufferGraph_GraphBuffersChanged;
            }
        }
    }
}
