// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using RoslynLspDiagnostic = Roslyn.LanguageServer.Protocol.Diagnostic;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteDiagnosticsService : IRemoteJsonService
{
    ValueTask<ImmutableArray<RoslynLspDiagnostic>> GetDiagnosticsAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        ImmutableArray<RoslynLspDiagnostic> csharpDiagnostics,
        ImmutableArray<RoslynLspDiagnostic> htmlDiagnostics,
        CancellationToken cancellationToken);
}
