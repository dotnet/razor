// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Runtime.CompilerServices;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

[Export(typeof(DocumentSnapshotFactory)), Shared]
[method: ImportingConstructor]
internal class DocumentSnapshotFactory(Lazy<ProjectSnapshotFactory> projectSnapshotFactory)
{
    private static readonly ConditionalWeakTable<TextDocument, RemoteDocumentSnapshot> s_documentSnapshots = new();

    private readonly Lazy<ProjectSnapshotFactory> _projectSnapshotFactory = projectSnapshotFactory;

    public RemoteDocumentSnapshot GetOrCreate(TextDocument textDocument)
    {
        lock (s_documentSnapshots)
        {
            if (!s_documentSnapshots.TryGetValue(textDocument, out var documentSnapshot))
            {
                var projectSnapshotFactory = _projectSnapshotFactory.Value;
                var projectSnapshot = projectSnapshotFactory.GetOrCreate(textDocument.Project);
                documentSnapshot = new RemoteDocumentSnapshot(textDocument, projectSnapshot, projectSnapshotFactory);
                s_documentSnapshots.Add(textDocument, documentSnapshot);
            }

            return documentSnapshot;
        }
    }
}
