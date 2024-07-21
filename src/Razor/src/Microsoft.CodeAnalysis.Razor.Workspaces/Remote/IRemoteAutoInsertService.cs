// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteAutoInsertService : IDisposable
{
    IEnumerable<string> TriggerCharacters {  get; }

    ValueTask<RemoteInsertTextEdit?> TryResolveInsertionAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId documentId,
        Position position,
        string character,
        bool autoCloseTags,
        CancellationToken cancellationToken);
}
