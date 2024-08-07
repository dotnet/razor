// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;

using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert.RemoteInsertTextEdit?>;

internal interface IRemoteAutoInsertService : IDisposable
{
    ValueTask<Response> TryResolveInsertionAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId documentId,
        Position position,
        string character,
        bool autoCloseTags,
        CancellationToken cancellationToken);
}
