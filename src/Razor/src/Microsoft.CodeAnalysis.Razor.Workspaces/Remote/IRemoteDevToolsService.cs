// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteDevToolsService
{
    ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse?> GetCSharpDocumentTextAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId razorDocumentId, CancellationToken cancellationToken);
    
    ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse?> GetHtmlDocumentTextAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId razorDocumentId, CancellationToken cancellationToken);
    
    ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.DocumentContentsResponse?> GetFormattingDocumentTextAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId razorDocumentId, CancellationToken cancellationToken);
    
    ValueTask<ImmutableArray<TagHelperDescriptor>> GetTagHelpersJsonAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId razorDocumentId, Microsoft.CodeAnalysis.Razor.Protocol.DevTools.TagHelpersKind kind, CancellationToken cancellationToken);
    
    ValueTask<Microsoft.CodeAnalysis.Razor.Protocol.DevTools.SyntaxVisualizerTree?> GetRazorSyntaxTreeAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId razorDocumentId, CancellationToken cancellationToken);
}