// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.CodeActions;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed partial class RemoteCodeActionsService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteCodeActionsService
{
    internal sealed class Factory : FactoryBase<IRemoteCodeActionsService>
    {
        protected override IRemoteCodeActionsService CreateService(in ServiceArgs args)
            => new RemoteCodeActionsService(in args);
    }

    private readonly ICodeActionsService _codeActionsService = args.ExportProvider.GetExportedValue<ICodeActionsService>();

    public ValueTask<CodeAction[]> GetCodeActionsAsync(JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo, JsonSerializableDocumentId razorDocumentId, CodeActionParams request, CancellationToken cancellationToken)
       => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetCodeActionsAsync(context, request, cancellationToken),
            cancellationToken);

    private ValueTask<CodeAction[]> GetCodeActionsAsync(RemoteDocumentContext context, CodeActionParams request, CancellationToken cancellationToken)
    {
        return new([]);
    }
}
