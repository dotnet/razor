// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using LspDiagnostic = Roslyn.LanguageServer.Protocol.Diagnostic;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteDiagnosticsService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteDiagnosticsService
{
    internal sealed class Factory : FactoryBase<IRemoteDiagnosticsService>
    {
        protected override IRemoteDiagnosticsService CreateService(in ServiceArgs args)
            => new RemoteDiagnosticsService(in args);
    }

    public ValueTask<ImmutableArray<LspDiagnostic>> GetDiagnosticsAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        ImmutableArray<LspDiagnostic> csharpDiagnostics,
        ImmutableArray<LspDiagnostic> htmlDiagnostics,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetDiagnosticsAsync(context, csharpDiagnostics, htmlDiagnostics, cancellationToken),
            cancellationToken);

    private async ValueTask<ImmutableArray<LspDiagnostic>> GetDiagnosticsAsync(
        RemoteDocumentContext context,
        ImmutableArray<LspDiagnostic> csharpDiagnostics,
        ImmutableArray<LspDiagnostic> htmlDiagnostics,
        CancellationToken cancellationToken)
    {
        // TODO: More work!
        return htmlDiagnostics;
    }
}
