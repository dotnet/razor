// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteAddIsolationFileService : IRemoteJsonService
{
    /// <summary>
    /// Creates an isolation file (CSS, C# code-behind, or JavaScript) for a Razor file.
    /// Returns a <see cref="WorkspaceEdit"/> containing CreateFile + TextDocumentEdit operations,
    /// or null if the operation could not be completed.
    /// </summary>
    ValueTask<WorkspaceEdit?> AddIsolationFileAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        Uri razorFileUri,
        string fileKind,
        CancellationToken cancellationToken);
}
