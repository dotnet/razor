// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal interface IRazorProjectService
{
    void AddDocument(string filePath);
    Task AddDocumentAsync(string filePath, CancellationToken cancellationToken);
    void OpenDocument(string filePath, SourceText sourceText, int version);
    Task OpenDocumentAsync(string filePath, SourceText sourceText, int version, CancellationToken cancellationToken);
    void UpdateDocument(string filePath, SourceText sourceText, int version);
    Task UpdateDocumentAsync(string filePath, SourceText sourceText, int version, CancellationToken cancellationToken);
    Task CloseDocumentAsync(string filePath, CancellationToken cancellationToken);
    Task RemoveDocumentAsync(string filePath, CancellationToken cancellationToken);

    ProjectKey AddProject(
        string filePath,
        string intermediateOutputPath,
        RazorConfiguration? configuration,
        string? rootNamespace,
        string? displayName = null);

    Task<ProjectKey> AddProjectAsync(
        string filePath,
        string intermediateOutputPath,
        RazorConfiguration? configuration,
        string? rootNamespace,
        string? displayName,
        CancellationToken cancellationToken);

    void UpdateProject(
        ProjectKey projectKey,
        RazorConfiguration? configuration,
        string? rootNamespace,
        string displayName,
        ProjectWorkspaceState projectWorkspaceState,
        ImmutableArray<DocumentSnapshotHandle> documents);
}
