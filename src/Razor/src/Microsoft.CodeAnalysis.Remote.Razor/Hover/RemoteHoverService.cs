// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Hover;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Roslyn.LanguageServer.Protocol;
using static Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Roslyn.LanguageServer.Protocol.Hover?>;
using static Microsoft.VisualStudio.LanguageServer.Protocol.VsLspExtensions;
using static Roslyn.LanguageServer.Protocol.RoslynLspExtensions;
using ExternalHandlers = Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;
using Range = Roslyn.LanguageServer.Protocol.Range;
using VsLsp = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteHoverService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteHoverService
{
    internal sealed class Factory : FactoryBase<IRemoteHoverService>
    {
        protected override IRemoteHoverService CreateService(in ServiceArgs args)
            => new RemoteHoverService(in args);
    }

    protected override IDocumentPositionInfoStrategy DocumentPositionInfoStrategy => PreferAttributeNameDocumentPositionInfoStrategy.Instance;

    public ValueTask<RemoteResponse<Hover?>> GetHoverAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        Position position,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetHoverAsync(context, position, cancellationToken),
            cancellationToken);

    private async ValueTask<RemoteResponse<Hover?>> GetHoverAsync(
        RemoteDocumentContext context,
        Position position,
        CancellationToken cancellationToken)
    {
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        if (!codeDocument.Source.Text.TryGetAbsoluteIndex(position, out var hostDocumentIndex))
        {
            return NoFurtherHandling;
        }

        var positionInfo = GetPositionInfo(codeDocument, hostDocumentIndex, preferCSharpOverHtml: true);

        if (positionInfo.LanguageKind is RazorLanguageKind.Html or RazorLanguageKind.Razor)
        {
            // Sometimes what looks like a html attribute can actually map to C#, in which case its better to let Roslyn try to handle this.
            if (!DocumentMappingService.TryMapToGeneratedDocumentPosition(codeDocument.GetCSharpDocument(), positionInfo.HostDocumentIndex, out _, out _))
            {
                // Acquire from client capabilities
                var options = new HoverDisplayOptions(VsLsp.MarkupKind.Markdown, SupportsVisualStudioExtensions: true);

                var razorHover = await HoverFactory
                    .GetHoverAsync(codeDocument, hostDocumentIndex, options, context.GetSolutionQueryOperations(), cancellationToken)
                    .ConfigureAwait(false);

                if (razorHover is null)
                {
                    return CallHtml;
                }

                // Ensure that we convert our Hover to a Roslyn Hover.
                var resultHover = ConvertHover(razorHover);

                return Results(resultHover);
            }
        }

        var csharpDocument = codeDocument.GetCSharpDocument();
        if (!DocumentMappingService.TryMapToGeneratedDocumentPosition(csharpDocument, positionInfo.HostDocumentIndex, out var mappedPosition, out _))
        {
            // If we can't map to the generated C# file, we're done.
            return NoFurtherHandling;
        }

        // Finally, call into C#.
        var generatedDocument = await context.Snapshot
            .GetGeneratedDocumentAsync(cancellationToken)
            .ConfigureAwait(false);

        var csharpHover = await ExternalHandlers.Hover
            .GetHoverAsync(generatedDocument, mappedPosition, supportsVSExtensions: true, supportsMarkdown: true, cancellationToken)
            .ConfigureAwait(false);

        if (csharpHover is null)
        {
            return NoFurtherHandling;
        }

        // Map range back to host document
        if (csharpHover.Range is { } range &&
            DocumentMappingService.TryMapToHostDocumentRange(csharpDocument, range.ToLinePositionSpan(), out var hostDocumentSpan))
        {
            csharpHover.Range = RoslynLspFactory.CreateRange(hostDocumentSpan);
        }

        return Results(csharpHover);
    }

    /// <summary>
    ///  Converts a <see cref="VsLsp.Hover"/> to a <see cref="Hover"/>.
    /// </summary>
    /// <remarks>
    ///  Once Razor moves wholly over to Roslyn.LanguageServer.Protocol, this method can be removed.
    /// </remarks>
    private Hover ConvertHover(VsLsp.Hover hover)
    {
        // Note: Razor only ever produces a Hover with MarkupContent or a VSInternalHover with RawContents.
        // Both variants return a Range.

        return hover switch
        {
            VsLsp.VSInternalHover { Range: var range, RawContent: { } rawContent } => new VSInternalHover()
            {
                Range = ConvertRange(range),
                Contents = string.Empty,
                RawContent = ConvertVsContent(rawContent)
            },
            VsLsp.Hover { Range: var range, Contents.Fourth: VsLsp.MarkupContent contents } => new Hover()
            {
                Range = ConvertRange(range),
                Contents = ConvertMarkupContent(contents)
            },
            _ => Assumed.Unreachable<Hover>(),
        };

        static Range? ConvertRange(VsLsp.Range? range)
        {
            return range is not null
                ? RoslynLspFactory.CreateRange(range.ToLinePositionSpan())
                : null;
        }

        static object ConvertVsContent(object obj)
        {
            return obj switch
            {
                VisualStudio.Core.Imaging.ImageId imageId => ConvertImageId(imageId),
                VisualStudio.Text.Adornments.ImageElement element => ConvertImageElement(element),
                VisualStudio.Text.Adornments.ClassifiedTextRun run => ConvertClassifiedTextRun(run),
                VisualStudio.Text.Adornments.ClassifiedTextElement element => ConvertClassifiedTextElement(element),
                VisualStudio.Text.Adornments.ContainerElement element => ConvertContainerElement(element),
                _ => Assumed.Unreachable<object>()
            };

            static Roslyn.Core.Imaging.ImageId ConvertImageId(VisualStudio.Core.Imaging.ImageId imageId)
            {
                return new(imageId.Guid, imageId.Id);
            }

            static Roslyn.Text.Adornments.ImageElement ConvertImageElement(VisualStudio.Text.Adornments.ImageElement element)
            {
                return new(ConvertImageId(element.ImageId), element.AutomationName);
            }

            static Roslyn.Text.Adornments.ClassifiedTextRun ConvertClassifiedTextRun(VisualStudio.Text.Adornments.ClassifiedTextRun run)
            {
                return new(
                    run.ClassificationTypeName,
                    run.Text,
                    (Roslyn.Text.Adornments.ClassifiedTextRunStyle)run.Style,
                    run.MarkerTagType,
                    run.NavigationAction,
                    run.Tooltip);
            }

            static Roslyn.Text.Adornments.ClassifiedTextElement ConvertClassifiedTextElement(VisualStudio.Text.Adornments.ClassifiedTextElement element)
            {
                return new(element.Runs.Select(ConvertClassifiedTextRun));
            }

            static Roslyn.Text.Adornments.ContainerElement ConvertContainerElement(VisualStudio.Text.Adornments.ContainerElement element)
            {
                return new((Roslyn.Text.Adornments.ContainerElementStyle)element.Style, element.Elements.Select(ConvertVsContent));
            }
        }

        static MarkupContent ConvertMarkupContent(VsLsp.MarkupContent value)
        {
            return new()
            {
                Kind = ConvertMarkupKind(value.Kind),
                Value = value.Value
            };
        }

        static MarkupKind ConvertMarkupKind(VsLsp.MarkupKind value)
        {
            return value == VsLsp.MarkupKind.Markdown
                ? MarkupKind.Markdown
                : MarkupKind.PlainText;
        }
    }
}
