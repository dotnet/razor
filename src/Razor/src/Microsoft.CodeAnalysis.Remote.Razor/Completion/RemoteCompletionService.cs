// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.Completion;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalCompletionList?>;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteCompletionService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteCompletionService
{
    internal sealed class Factory : FactoryBase<IRemoteCompletionService>
    {
        protected override IRemoteCompletionService CreateService(in ServiceArgs args)
            => new RemoteCompletionService(in args);
    }

    private readonly RazorCompletionListProvider _razorCompletionListProvider = args.ExportProvider.GetExportedValue<RazorCompletionListProvider>();

    public ValueTask<Response> GetCompletionAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        Position position,
        CompletionContext completionContext,
        VSInternalClientCapabilities clientCapabilities,
        RazorCompletionOptions razorCompletionOptions,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetCompletionAsync(context, position, completionContext, clientCapabilities, razorCompletionOptions, cancellationToken),
            cancellationToken);

    private async ValueTask<Response> GetCompletionAsync(
        RemoteDocumentContext remoteDocumentContext,
        Position position,
        CompletionContext completionContext,
        VSInternalClientCapabilities clientCapabilities,
        RazorCompletionOptions razorCompletionOptions,
        CancellationToken cancellationToken)
    {
        var sourceText = await remoteDocumentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (!sourceText.TryGetAbsoluteIndex(position, out var index))
        {
            return Response.NoFurtherHandling;
        }

        var codeDocument = await remoteDocumentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        var positionInfo = GetPositionInfo(codeDocument, index);
        var languageKind = positionInfo.LanguageKind;

        if (languageKind == RazorLanguageKind.CSharp)
        {
            var mappedPosition = positionInfo.Position.ToLinePosition();
            var csharpCompletion = await GetCSharpCompletionAsync(
                    remoteDocumentContext,
                    mappedPosition,
                    completionContext,
                    cancellationToken);
        }

        var vsInternalCompletionContext = new VSInternalCompletionContext()
        {
            InvokeKind = completionContext.TriggerCharacter != null ? VSInternalCompletionInvokeKind.Typing : VSInternalCompletionInvokeKind.Explicit,
            TriggerKind = completionContext.TriggerKind,
            TriggerCharacter = completionContext.TriggerCharacter
        };

        var completionList = await _razorCompletionListProvider.GetCompletionListAsync(
            index,
            vsInternalCompletionContext,
            remoteDocumentContext,
            clientCapabilities,
            existingCompletions: new HashSet<string>(), // TODO: use existing data
            razorCompletionOptions,
            cancellationToken);

        if (completionList is null)
        {
            return Response.CallHtml;
        }
        else
        {
            return Response.Results(completionList);
        }
    }

    private async ValueTask<VSInternalCompletionList?> GetCSharpCompletionAsync(
        RemoteDocumentContext remoteDocumentContext,
        LinePosition mappedPosition,
        CompletionContext completionContext,
        CancellationToken cancellationToken)
    {
        // TODO: hookup Roslyn API

        return new VSInternalCompletionList();
    }
}
