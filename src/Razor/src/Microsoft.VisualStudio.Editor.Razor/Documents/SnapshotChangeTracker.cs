﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Editor.Razor.Documents;

// See ReiteratedVersionSnapshotTracker in dotnet/Roslyn -- this is primarily here for the
// side-effect of making sure the last 'reiterated' snapshot is retained in memory.
//
// Since we're interacting with the workspace in the same way, we're doing the same thing.
internal class SnapshotChangeTracker
{
    private ITextBuffer? _textBuffer;
    private ITextSnapshot? _snapshot;

    public void StartTracking(ITextBuffer? buffer)
    {
        // buffer has changed. stop tracking old buffer
        if (_textBuffer is not null && buffer != _textBuffer)
        {
            _textBuffer.ChangedHighPriority -= OnTextBufferChanged;

            _textBuffer = null;
            _snapshot = null;
        }

        // start tracking new buffer
        if (buffer is not null && _snapshot is null)
        {
            _snapshot = buffer.CurrentSnapshot;
            _textBuffer = buffer;

            buffer.ChangedHighPriority += OnTextBufferChanged;
        }
    }

    public void StopTracking(ITextBuffer? buffer)
    {
        if (_textBuffer == buffer && buffer is not null && _snapshot is not null)
        {
            buffer.ChangedHighPriority -= OnTextBufferChanged;

            _textBuffer = null;
            _snapshot = null;
        }
    }

    private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
    {
        if (sender is ITextBuffer)
        {
            var snapshot = _snapshot;
            if (snapshot is not null && snapshot.Version is not null && e.AfterVersion is not null &&
                snapshot.Version.ReiteratedVersionNumber < e.AfterVersion.ReiteratedVersionNumber)
            {
                _snapshot = e.After;
            }
        }
    }
}
