﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Diagnostics;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteDiagnosticsService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteDiagnosticsService
{
    internal sealed class Factory : FactoryBase<IRemoteDiagnosticsService>
    {
        protected override IRemoteDiagnosticsService CreateService(in ServiceArgs args)
            => new RemoteDiagnosticsService(in args);
    }

    private readonly RazorTranslateDiagnosticsService _translateDiagnosticsService = args.ExportProvider.GetExportedValue<RazorTranslateDiagnosticsService>();

    public ValueTask<ImmutableArray<LspDiagnostic>> GetDiagnosticsAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        LspDiagnostic[] csharpDiagnostics,
        LspDiagnostic[] htmlDiagnostics,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetDiagnosticsAsync(context, csharpDiagnostics, htmlDiagnostics, cancellationToken),
            cancellationToken);

    private async ValueTask<ImmutableArray<LspDiagnostic>> GetDiagnosticsAsync(
        RemoteDocumentContext context,
        LspDiagnostic[] csharpDiagnostics,
        LspDiagnostic[] htmlDiagnostics,
        CancellationToken cancellationToken)
    {
        // We've got C# and Html, lets get Razor diagnostics
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        // Yes, CSharpDocument.Documents are the Razor diagnostics. Don't ask.
        var razorDiagnostics = codeDocument.GetRequiredCSharpDocument().Diagnostics;

        return [
            .. RazorDiagnosticHelper.Convert(razorDiagnostics, codeDocument.Source.Text, context.Snapshot),
            .. await _translateDiagnosticsService.TranslateAsync(RazorLanguageKind.CSharp, csharpDiagnostics, context.Snapshot, cancellationToken).ConfigureAwait(false),
            .. await _translateDiagnosticsService.TranslateAsync(RazorLanguageKind.Html, htmlDiagnostics, context.Snapshot, cancellationToken).ConfigureAwait(false)
        ];
    }

    public ValueTask<ImmutableArray<LspDiagnostic>> GetTaskListDiagnosticsAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        ImmutableArray<string> taskListDescriptors,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetTaskListDiagnosticsAsync(context, taskListDescriptors, cancellationToken),
            cancellationToken);

    private static async ValueTask<ImmutableArray<LspDiagnostic>> GetTaskListDiagnosticsAsync(
        RemoteDocumentContext context,
        ImmutableArray<string> taskListDescriptors,
        CancellationToken cancellationToken)
    {
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        return TaskListDiagnosticProvider.GetTaskListDiagnostics(codeDocument, taskListDescriptors);
    }
}
