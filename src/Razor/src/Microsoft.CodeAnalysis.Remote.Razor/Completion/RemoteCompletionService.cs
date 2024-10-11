// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalCompletionList?>;
using RoslynCompletionContext = Roslyn.LanguageServer.Protocol.CompletionContext;
using RoslynCompletionSetting = Roslyn.LanguageServer.Protocol.CompletionSetting;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteCompletionService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteCompletionService
{
    internal sealed class Factory : FactoryBase<IRemoteCompletionService>
    {
        protected override IRemoteCompletionService CreateService(in ServiceArgs args)
            => new RemoteCompletionService(in args);
    }

    private readonly RazorCompletionListProvider _razorCompletionListProvider = args.ExportProvider.GetExportedValue<RazorCompletionListProvider>();

    public ValueTask<DocumentPositionInfo?> GetPositionInfoAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        Position position,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetPositionInfoAsync(context, position, cancellationToken),
            cancellationToken);

    private async ValueTask<DocumentPositionInfo?> GetPositionInfoAsync(
        RemoteDocumentContext remoteDocumentContext,
        Position position,
        CancellationToken cancellationToken)
    {
        var sourceText = await remoteDocumentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (!sourceText.TryGetAbsoluteIndex(position, out var index))
        {
            return null;
        }

        var codeDocument = await remoteDocumentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        return GetPositionInfo(codeDocument, index);
    }

    public ValueTask<Response> GetCompletionAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        DocumentPositionInfo positionInfo,
        CompletionContext completionContext,
        VSInternalClientCapabilities clientCapabilities,
        RazorCompletionOptions razorCompletionOptions,
        HashSet<string> existingHtmlCompletions,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetCompletionAsync(
                context,
                positionInfo,
                completionContext,
                clientCapabilities,
                razorCompletionOptions,
                existingHtmlCompletions,
                cancellationToken),
            cancellationToken);

    private async ValueTask<Response> GetCompletionAsync(
        RemoteDocumentContext remoteDocumentContext,
        DocumentPositionInfo positionInfo,
        CompletionContext completionContext,
        VSInternalClientCapabilities clientCapabilities,
        RazorCompletionOptions razorCompletionOptions,
        HashSet<string> existingDelegatedCompletions,
        CancellationToken cancellationToken)
    {
        VSInternalCompletionList? csharpCompletionList = null;
        if (positionInfo.LanguageKind == RazorLanguageKind.CSharp &&
            CompletionTriggerCharacters.IsValidTrigger(CompletionTriggerCharacters.CSharpTriggerCharacters, completionContext))
        {
            var mappedPosition = positionInfo.Position.ToLinePosition();
            csharpCompletionList = await GetCSharpCompletionAsync(
                    remoteDocumentContext,
                    mappedPosition,
                    completionContext,
                    clientCapabilities,
                    cancellationToken);

            if (csharpCompletionList is not null)
            {
                Debug.Assert(existingDelegatedCompletions.Count == 0, "Delegated completion should be either C# or HTML, not both");
                existingDelegatedCompletions.UnionWith(csharpCompletionList.Items.Select((item) => item.Label));
            }
        }

        var vsInternalCompletionContext = new VSInternalCompletionContext()
        {
            InvokeKind = completionContext.TriggerCharacter != null ? VSInternalCompletionInvokeKind.Typing : VSInternalCompletionInvokeKind.Explicit,
            TriggerKind = completionContext.TriggerKind,
            TriggerCharacter = completionContext.TriggerCharacter
        };

        var razorCompletionList = CompletionTriggerCharacters.IsValidTrigger(CompletionTriggerCharacters.RazorTriggerCharacters, completionContext)
            ? await _razorCompletionListProvider.GetCompletionListAsync(
                positionInfo.HostDocumentIndex,
                vsInternalCompletionContext,
                remoteDocumentContext,
                clientCapabilities,
                existingCompletions: existingDelegatedCompletions,
                razorCompletionOptions,
                cancellationToken)
            : null;

        if (CompletionListMerger.Merge(razorCompletionList, csharpCompletionList) is not { } mergedCompletionList)
        {
            return Response.CallHtml;
        }

        return Response.Results(mergedCompletionList);
    }

    private async ValueTask<VSInternalCompletionList?> GetCSharpCompletionAsync(
        RemoteDocumentContext remoteDocumentContext,
        LinePosition mappedPosition,
        CompletionContext completionContext,
        VSInternalClientCapabilities clientCapabilities,
        CancellationToken cancellationToken)
    {
        var generatedDocument = await remoteDocumentContext.Snapshot.GetGeneratedDocumentAsync().ConfigureAwait(false);

        // This is, to say the least, not ideal. In future we're going to normalize on to Roslyn LSP types, and this can go.
        var options = new JsonSerializerOptions();
        foreach (var converter in RazorServiceDescriptorsWrapper.GetLspConverters())
        {
            options.Converters.Add(converter);
        }

        if (JsonSerializer.Deserialize<RoslynCompletionContext>(JsonSerializer.SerializeToDocument(completionContext), options) is not { } roslynCompletionContext)
        {
            return null;
        }

        if (JsonSerializer.Deserialize<RoslynCompletionSetting>(JsonSerializer.SerializeToDocument(clientCapabilities.TextDocument?.Completion), options) is not { } roslynCompletionSetting)
        {
            return null;
        }

        var roslynCompletionList = await ExternalAccess.Razor.Cohost.Handlers.Completion.GetCompletionListAsync(
            generatedDocument,
            mappedPosition,
            roslynCompletionContext,
            clientCapabilities.SupportsVisualStudioExtensions,
            roslynCompletionSetting,
            cancellationToken);

        if (roslynCompletionList is null)
        {
            return null;
        }

        var vsPlatformCompletionList = JsonSerializer.Deserialize<VSInternalCompletionList>(JsonSerializer.SerializeToDocument(roslynCompletionList), options);

        return vsPlatformCompletionList;
    }
}
