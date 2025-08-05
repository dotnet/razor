// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Microsoft.CodeAnalysis.LanguageServer.Protocol.CodeLens[]?>;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed partial class RemoteCodeLensService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteCodeLensService
{
    internal sealed class Factory : FactoryBase<IRemoteCodeLensService>
    {
        protected override IRemoteCodeLensService CreateService(in ServiceArgs args)
            => new RemoteCodeLensService(in args);
    }

    public ValueTask<Response> GetCodeLensAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetCodeLensAsync(context, cancellationToken),
            cancellationToken);

    private static async ValueTask<Response> GetCodeLensAsync(
        RemoteDocumentContext context,
        CancellationToken cancellationToken)
    {
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var csharpDocument = codeDocument.GetCSharpDocument();

        if (csharpDocument is null)
        {
            return Response.Results(null);
        }

        // TODO: Once CodeLens support is available in Roslyn cohosting (ExternalHandlers.CodeLens),
        // call into Roslyn to get C# CodeLens results and map them from C# to Razor coordinates
        // using the Document Mapping Service.
        //
        // For now, return an empty result to establish the infrastructure.
        // This can be expanded when CodeLens support is added to ExternalHandlers.

        return Response.Results(null);
    }
}