// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Test.Common.Editor;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Razor.LanguageClient;

internal sealed class TestDocumentManager : TrackingLSPDocumentManager
{
    private readonly Dictionary<DocumentUri, LSPDocumentSnapshot> _documents = [];

    public int UpdateVirtualDocumentCallCount { get; private set; }

    public override bool TryGetDocument(Uri uri, out LSPDocumentSnapshot lspDocumentSnapshot)
        => TryGetDocument(new DocumentUri(uri), out lspDocumentSnapshot);

    public bool TryGetDocument(DocumentUri uri, out LSPDocumentSnapshot lspDocumentSnapshot)
    {
        return _documents.TryGetValue(uri, out lspDocumentSnapshot);
    }

    public void AddDocument(DocumentUri uri, LSPDocumentSnapshot documentSnapshot)
    {
        _documents.Add(uri, documentSnapshot);
    }

    public override void TrackDocument(ITextBuffer buffer)
    {
        throw new NotImplementedException();
    }

    public override void UntrackDocument(ITextBuffer buffer)
    {
        throw new NotImplementedException();
    }

    public override void UpdateVirtualDocument<TVirtualDocument>(Uri hostUri, IReadOnlyList<ITextChange> changes, int hostDocumentVersion, object? state)
    {
        var hostDocumentUri = new DocumentUri(hostUri);
        if (!_documents.TryGetValue(hostDocumentUri, out var documentSnapshot))
        {
            return;
        }

        if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var virtualDocumentSnapshot))
        {
            return;
        }

        using var virtualDocument = new CSharpVirtualDocument(projectKey: default, virtualDocumentSnapshot.Uri, virtualDocumentSnapshot.Snapshot.TextBuffer, NoOpTelemetryReporter.Instance);
        virtualDocument.Update(changes, hostDocumentVersion, state);
        _documents[hostDocumentUri] = new TestLSPDocumentSnapshot(hostDocumentUri, documentSnapshot.Version, new[] { virtualDocument.CurrentSnapshot });

        UpdateVirtualDocumentCallCount++;
    }
}
