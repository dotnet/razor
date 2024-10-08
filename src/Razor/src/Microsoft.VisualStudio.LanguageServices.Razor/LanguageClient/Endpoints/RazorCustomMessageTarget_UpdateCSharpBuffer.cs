﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Endpoints;

internal partial class RazorCustomMessageTarget
{
    // Called by the Razor Language Server to update the contents of the virtual CSharp buffer.
    [JsonRpcMethod(CustomMessageNames.RazorUpdateCSharpBufferEndpoint, UseSingleObjectParameterDeserialization = true)]
    public async Task UpdateCSharpBufferAsync(UpdateBufferRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        await UpdateCSharpBufferCoreAsync(request, cancellationToken);
    }

    // Internal for testing
    internal async Task UpdateCSharpBufferCoreAsync(UpdateBufferRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.HostDocumentFilePath is null || request.HostDocumentVersion is null)
        {
            return;
        }

        var hostDocumentUri = new Uri(request.HostDocumentFilePath);

        _logger.LogDebug($"UpdateCSharpBuffer for {request.HostDocumentVersion} of {hostDocumentUri} in {request.ProjectKeyId}");

        // If we're generating unique file paths for virtual documents, then we have to take a different path here, and do more work
        if (_languageServerFeatureOptions.IncludeProjectKeyInGeneratedFilePath &&
            request.ProjectKeyId is not null &&
            _documentManager.TryGetDocument(hostDocumentUri, out var documentSnapshot) &&
            documentSnapshot.TryGetAllVirtualDocuments<CSharpVirtualDocumentSnapshot>(out var virtualDocuments))
        {
            if (virtualDocuments is [{ ProjectKey.IsUnknown: true }])
            {
                // If there is only a single virtual document, and its got a null id, then that means it's in our "misc files" project
                // but the server clearly knows about it in a real project. That means its probably new, as Visual Studio opens a buffer
                // for a document before we get the notifications about it being added to any projects. Lets try refreshing before
                // we worry.
                _logger.LogDebug($"Refreshing virtual documents, and waiting for them, (for {hostDocumentUri})");

                var task = _csharpVirtualDocumentAddListener.WaitForDocumentAddAsync(cancellationToken);
                _documentManager.RefreshVirtualDocuments();
                var added = await task.ConfigureAwait(true);

                // Since we're dealing with snapshots, we have to get the new ones after refreshing
                if (!_documentManager.TryGetDocument(hostDocumentUri, out documentSnapshot) ||
                    !documentSnapshot.TryGetAllVirtualDocuments<CSharpVirtualDocumentSnapshot>(out virtualDocuments))
                {
                    // This should never happen.
                    // The server clearly wants to tell us about a document in a project, but we don't know which project it's in.
                    // Sadly there isn't anything we can do here to, we're just in a state where the server and client are out of
                    // sync with their understanding of the document contents, and since changes come in as a list of changes,
                    // the user experience is broken. All we can do is hope the user closes and re-opens the document.
                    Debug.Fail($"Server wants to update {hostDocumentUri} in {request.ProjectKeyId} but we don't know about the document being in any projects");
                    _logger.LogError($"Server wants to update {hostDocumentUri} in {request.ProjectKeyId} by we only know about that document in misc files. Server and client are now out of sync.");
                    return;
                }
            }

            foreach (var virtualDocument in virtualDocuments)
            {
                if (virtualDocument.ProjectKey.Equals(new ProjectKey(request.ProjectKeyId)))
                {
                    _logger.LogDebug($"UpdateCSharpBuffer virtual doc for {request.HostDocumentVersion} of {virtualDocument.Uri}. Previous lines: {virtualDocument.Snapshot.LineCount}");

                    _documentManager.UpdateVirtualDocument<CSharpVirtualDocument>(
                        hostDocumentUri,
                        virtualDocument.Uri,
                        request.Changes.Select(change => change.ToVisualStudioTextChange()).ToArray(),
                        request.HostDocumentVersion.Value,
                        state: request.PreviousWasEmpty);

                    _logger.LogDebug($"UpdateCSharpBuffer finished updating doc for {request.HostDocumentVersion} of {virtualDocument.Uri}. New lines: {GetLineCountOfVirtualDocument(hostDocumentUri, virtualDocument)}");

                    return;
                }
            }

            if (virtualDocuments.Length > 1)
            {
                // If the particular document supports multiple virtual documents, we don't want to try to update a single one. The server could
                // be sending C# for a Misc Files file, but once the server knows about the real project, it will start sending C# for that, and
                // that needs to be a brand new buffer.
                _logger.LogDebug($"""
                    Was looking for {request.ProjectKeyId} but found only:
                    {string.Join(Environment.NewLine, virtualDocuments.Select(d => $"[{d.ProjectKey}] {d.Uri}"))}
                    """);
            }

            _logger.LogDebug($"UpdateCSharpBuffer couldn't find any virtual docs for {request.HostDocumentVersion} of {hostDocumentUri}");

            // Don't know about document, no-op. This can happen if the language server found a project.razor.bin from an old build
            // and is sending us updates.
            return;
        }

        _logger.LogDebug($"UpdateCSharpBuffer fallback for {request.HostDocumentVersion} of {hostDocumentUri}");

        _documentManager.UpdateVirtualDocument<CSharpVirtualDocument>(
            hostDocumentUri,
            request.Changes.Select(change => change.ToVisualStudioTextChange()).ToArray(),
            request.HostDocumentVersion.Value,
            state: request.PreviousWasEmpty);
    }

    private int GetLineCountOfVirtualDocument(Uri hostDocumentUri, CSharpVirtualDocumentSnapshot virtualDocument)
    {
        if (_documentManager.TryGetDocument(hostDocumentUri, out var newDocSnapshot) &&
            newDocSnapshot.VirtualDocuments.FirstOrDefault(e => e.Uri == virtualDocument.Uri) is { } newDoc)
        {
            return newDoc.Snapshot.LineCount;
        }

        return -1;
    }
}
