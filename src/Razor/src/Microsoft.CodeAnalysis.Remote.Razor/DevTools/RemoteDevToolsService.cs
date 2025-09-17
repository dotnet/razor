// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteDevToolsService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteDevToolsService
{
    internal sealed class Factory : FactoryBase<IRemoteDevToolsService>
    {
        protected override IRemoteDevToolsService CreateService(in ServiceArgs args)
            => new RemoteDevToolsService(in args);
    }

    public ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse?> GetCSharpDocumentTextAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetCSharpDocumentTextAsync(context, cancellationToken),
            cancellationToken);

    public ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse?> GetHtmlDocumentTextAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetHtmlDocumentTextAsync(context, cancellationToken),
            cancellationToken);

    public ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse?> GetFormattingDocumentTextAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetFormattingDocumentTextAsync(context, cancellationToken),
            cancellationToken);

    public ValueTask<string> GetTagHelpersJsonAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        Microsoft.CodeAnalysis.Razor.Protocol.DevTools.TagHelpersKind kind,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetTagHelpersJsonAsync(context, kind, cancellationToken),
            cancellationToken);

    public ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.RazorSyntaxTree?> GetRazorSyntaxTreeAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetRazorSyntaxTreeAsync(context, cancellationToken),
            cancellationToken);

    private async ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse?> GetCSharpDocumentTextAsync(RemoteDocumentContext documentContext, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var contents = codeDocument.GetCSharpSourceText().ToString();
        var filePath = documentContext.Snapshot.FilePath + ".g.cs";
        
        return new Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse
        {
            Contents = contents,
            FilePath = filePath
        };
    }

    private async ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse?> GetHtmlDocumentTextAsync(RemoteDocumentContext documentContext, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var contents = codeDocument.GetHtmlSourceText().ToString();
        var filePath = documentContext.Snapshot.FilePath + ".g.html";
        
        return new Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse
        {
            Contents = contents,
            FilePath = filePath
        };
    }

    private async ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse?> GetFormattingDocumentTextAsync(RemoteDocumentContext documentContext, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
#pragma warning disable CS0618 // Type or member is obsolete
        var contents = CSharpFormattingPass.GetFormattingDocumentContentsForSyntaxVisualizer(codeDocument);
#pragma warning restore CS0618 // Type or member is obsolete
        var filePath = documentContext.Snapshot.FilePath + ".formatting.cs";
        
        return new Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse
        {
            Contents = contents,
            FilePath = filePath
        };
    }

    private async ValueTask<string> GetTagHelpersJsonAsync(RemoteDocumentContext documentContext, Microsoft.CodeAnalysis.Razor.Protocol.DevTools.TagHelpersKind kind, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var tagHelpers = kind switch
        {
            Microsoft.CodeAnalysis.Razor.Protocol.DevTools.TagHelpersKind.All => codeDocument.GetTagHelpers(),
            Microsoft.CodeAnalysis.Razor.Protocol.DevTools.TagHelpersKind.InScope => codeDocument.GetRequiredTagHelperContext().TagHelpers,
            Microsoft.CodeAnalysis.Razor.Protocol.DevTools.TagHelpersKind.Referenced => (IEnumerable<TagHelperDescriptor>?)codeDocument.GetReferencedTagHelpers(),
            _ => []
        };

        tagHelpers ??= [];
        return JsonSerializer.Serialize(tagHelpers, new JsonSerializerOptions { WriteIndented = true });
    }

    private async ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.RazorSyntaxTree?> GetRazorSyntaxTreeAsync(RemoteDocumentContext documentContext, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var razorSyntaxTree = codeDocument.GetSyntaxTree();
        
        if (razorSyntaxTree?.Root == null)
            return null;

        return new Microsoft.CodeAnalysis.Razor.Protocol.DevTools.RazorSyntaxTree
        {
            Root = ConvertSyntaxNode(razorSyntaxTree.Root)
        };
    }

    private static Microsoft.CodeAnalysis.Razor.Protocol.DevTools.RazorSyntaxNode ConvertSyntaxNode(SyntaxNode node)
    {
        var children = node.ChildNodes().Select(ConvertSyntaxNode).ToArray();
        
        return new Microsoft.CodeAnalysis.Razor.Protocol.DevTools.RazorSyntaxNode
        {
            Kind = node.Kind.ToString(),
            SpanStart = node.SpanStart,
            SpanEnd = node.Span.End,
            Children = children
        };
    }
}