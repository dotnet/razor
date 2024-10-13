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
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.Completion;
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

    public ValueTask<CompletionPositionInfo?> GetPositionInfoAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        VSInternalCompletionContext completionContext,
        Position position,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetPositionInfoAsync(context, completionContext, position, cancellationToken),
            cancellationToken);

    private async ValueTask<CompletionPositionInfo?> GetPositionInfoAsync(
        RemoteDocumentContext remoteDocumentContext,
        VSInternalCompletionContext completionContext,
        Position position,
        CancellationToken cancellationToken)
    {
        var sourceText = await remoteDocumentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (!sourceText.TryGetAbsoluteIndex(position, out var index))
        {
            return null;
        }

        var codeDocument = await remoteDocumentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        var positionInfo = GetPositionInfo(codeDocument, index);

        if(positionInfo.LanguageKind != RazorLanguageKind.Razor
           && await DelegatedCompletionHelper.TryGetProvisionalCompletionInfoAsync(
                remoteDocumentContext,
                completionContext,
                positionInfo,
                DocumentMappingService,
                cancellationToken) is { } provisionalCompletionInfo)
        {
            return new CompletionPositionInfo()
            {
                DocumentPositionInfo = provisionalCompletionInfo.DocumentPositionInfo,
                ProvisionalTextEdit = provisionalCompletionInfo.ProvisionalTextEdit
            };
        }

        return new CompletionPositionInfo() { DocumentPositionInfo = positionInfo };
    }

    public ValueTask<Response> GetCompletionAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        CompletionPositionInfo positionInfo,
        VSInternalCompletionContext completionContext,
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
        CompletionPositionInfo positionInfo,
        VSInternalCompletionContext completionContext,
        VSInternalClientCapabilities clientCapabilities,
        RazorCompletionOptions razorCompletionOptions,
        HashSet<string> existingDelegatedCompletions,
        CancellationToken cancellationToken)
    {
        VSInternalCompletionList? csharpCompletionList = null;
        var documentPositionInfo = positionInfo.DocumentPositionInfo;
        if (documentPositionInfo.LanguageKind == RazorLanguageKind.CSharp &&
            CompletionTriggerCharacters.IsValidTrigger(CompletionTriggerCharacters.CSharpTriggerCharacters, completionContext))
        {
            var mappedPosition = documentPositionInfo.Position.ToLinePosition();
            csharpCompletionList = await GetCSharpCompletionAsync(
                    remoteDocumentContext,
                    mappedPosition,
                    positionInfo.ProvisionalTextEdit,
                    completionContext,
                    clientCapabilities,
                    cancellationToken);

            if (csharpCompletionList is not null)
            {
                Debug.Assert(existingDelegatedCompletions.Count == 0, "Delegated completion should be either C# or HTML, not both");
                existingDelegatedCompletions.UnionWith(csharpCompletionList.Items.Select((item) => item.Label));
            }
        }

        var razorCompletionList = CompletionTriggerCharacters.IsValidTrigger(CompletionTriggerCharacters.RazorTriggerCharacters, completionContext)
            ? await _razorCompletionListProvider.GetCompletionListAsync(
                documentPositionInfo.HostDocumentIndex,
                completionContext,
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
        TextEdit? provisionalTextEdit,
        CompletionContext completionContext,
        VSInternalClientCapabilities clientCapabilities,
        CancellationToken cancellationToken)
    {
        var generatedDocument = await remoteDocumentContext.Snapshot.GetGeneratedDocumentAsync().ConfigureAwait(false);
        if (provisionalTextEdit is not null)
        {
            var generatedText = await generatedDocument.GetTextAsync(cancellationToken);
            var startIndex = generatedText.GetPosition(provisionalTextEdit.Range.Start);
            var endIndex = generatedText.GetPosition(provisionalTextEdit.Range.End);
            var generatedTextWithEdit = generatedText.Replace(startIndex, length: endIndex - startIndex, newText: provisionalTextEdit.NewText);
            generatedDocument = generatedDocument.WithText(generatedTextWithEdit);
        }

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
