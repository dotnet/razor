// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
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

    public ValueTask<string?> GetCSharpDocumentTextAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetCSharpDocumentTextAsync(context, cancellationToken),
            cancellationToken);

    public ValueTask<string?> GetHtmlDocumentTextAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetHtmlDocumentTextAsync(context, cancellationToken),
            cancellationToken);

    public ValueTask<string?> GetFormattingDocumentTextAsync(
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
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetTagHelpersJsonAsync(context, cancellationToken),
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

    private async ValueTask<string?> GetCSharpDocumentTextAsync(RemoteDocumentContext documentContext, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        return codeDocument.GetCSharpSourceText().ToString();
    }

    private async ValueTask<string?> GetHtmlDocumentTextAsync(RemoteDocumentContext documentContext, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        return codeDocument.GetHtmlSourceText().ToString();
    }

    private async ValueTask<string?> GetFormattingDocumentTextAsync(RemoteDocumentContext documentContext, CancellationToken cancellationToken)
    {
        // For formatting, we typically want the C# generated document
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        return codeDocument.GetCSharpSourceText().ToString();
    }

    private async ValueTask<string> GetTagHelpersJsonAsync(RemoteDocumentContext documentContext, CancellationToken cancellationToken)
    {
        var tagHelperDescriptors = await documentContext.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(tagHelperDescriptors, new JsonSerializerOptions { WriteIndented = true });
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

    private static Microsoft.CodeAnalysis.Razor.Protocol.DevTools.RazorSyntaxNode ConvertSyntaxNode(AspNetCore.Razor.Language.Syntax.SyntaxNode node)
    {
        var children = node.ChildNodes().Select(ConvertSyntaxNode).ToArray();
        
        return new Microsoft.CodeAnalysis.Razor.Protocol.DevTools.RazorSyntaxNode
        {
            Kind = node.Kind.ToString(),
            SpanStart = node.SpanStart,
            SpanEnd = node.Span.End,
            SpanLength = node.Span.Length,
            Children = children
        };
    }
}