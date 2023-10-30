// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal abstract class GeneratedDocumentPublisher : IProjectSnapshotChangeTrigger
{
    public abstract void Initialize(ProjectSnapshotManagerBase projectManager);

    public abstract ValueTask PublishCSharpAsync(
        ProjectKey projectKey,
        string filePath,
        SourceText text,
        int hostDocumentVersion,
        CancellationToken cancellationToken);

    public abstract ValueTask PublishHtmlAsync(
        ProjectKey projectKey,
        string filePath,
        SourceText text,
        int hostDocumentVersion,
        CancellationToken cancellationToken);
}
