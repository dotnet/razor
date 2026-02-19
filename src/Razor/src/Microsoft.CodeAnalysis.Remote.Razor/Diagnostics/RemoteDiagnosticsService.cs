// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Diagnostics;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using EAConstants = Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Constants;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteDiagnosticsService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteDiagnosticsService
{
    private const string UnusedDirectiveDiagnosticId = "RZ0005";
    private static readonly DiagnosticTag[] s_unnecessaryDiagnosticTags = [VSDiagnosticTags.HiddenInEditor, DiagnosticTag.Unnecessary];

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

        ImmutableArray<LspDiagnostic> allDiagnostics = [
            .. GetRazorDiagnostics(context, codeDocument),
            .. await _translateDiagnosticsService.TranslateAsync(RazorLanguageKind.CSharp, csharpDiagnostics, context.Snapshot, cancellationToken).ConfigureAwait(false),
            .. await _translateDiagnosticsService.TranslateAsync(RazorLanguageKind.Html, htmlDiagnostics, context.Snapshot, cancellationToken).ConfigureAwait(false)
        ];

        // Our final pass is to update all unused directive errors to ensure they display how we want in the IDE. Doing it here
        // means we don't have to duplicate between where we raise our own diagnostics, and filter Roslyns.
        foreach (var diagnostic in allDiagnostics)
        {
            if (diagnostic.Code is { Value: EAConstants.DiagnosticIds.IDE0005_gen })
            {
                diagnostic.Severity = LspDiagnosticSeverity.Warning;
                diagnostic.Tags = s_unnecessaryDiagnosticTags;
                diagnostic.Code = UnusedDirectiveDiagnosticId;

                // If Roslyn reports the diagnostic, we'll map the C# to Razor, and it will be just the
                // "using <namespace>" part, and not the "@". Again, its simplest to just adjust that here
                if (diagnostic.Range.Start.Character > 0)
                {
                    diagnostic.Range.Start.Character = 0;
                }
            }
        }

        return allDiagnostics;
    }

    private static ImmutableArray<LspDiagnostic> GetRazorDiagnostics(RemoteDocumentContext context, RazorCodeDocument codeDocument)
    {
        using var diagnostics = new PooledArrayBuilder<LspDiagnostic>();

        // First, RZ diagnostics. Yes, CSharpDocument.Documents are the Razor diagnostics. Don't ask.
        var razorDiagnostics = codeDocument.GetRequiredCSharpDocument().Diagnostics;
        diagnostics.AddRange(RazorDiagnosticHelper.Convert(razorDiagnostics, codeDocument.Source.Text, context.Snapshot));

        // For legacy files, that aren't imports, we also want to raise unused directive diagnostics. We only do this for
        // legacy (ie, @addTagHelper directives) because for .razor files we get the diagnostics from Roslyn, and filter
        // them out in the RazorTranslateDiagnosticsService.
        if (codeDocument.FileKind.IsLegacy() && !codeDocument.IsImportsFile())
        {
            var syntaxTree = codeDocument.GetRequiredSyntaxTree();
            var sourceText = codeDocument.Source.Text;

            foreach (var directive in syntaxTree.EnumerateAddTagHelperDirectives())
            {
                if (codeDocument.IsDirectiveUsed(directive))
                {
                    continue;
                }

                diagnostics.Add(new LspDiagnostic
                {
                    // We log the same as Roslyn does, so we can have only one post-report cleanup pass, above.
                    Code = EAConstants.DiagnosticIds.IDE0005_gen,
                    Message = "@addTagHelper directive is unnecessary.",
                    Source = "Razor",
                    Range = sourceText.GetRange(directive.SpanWithoutTrailingNewLines(sourceText)),
                });
            }
        }

        return diagnostics.ToImmutableAndClear();
    }

    public ValueTask<ImmutableArray<LspDiagnostic>> GetTaskListDiagnosticsAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        ImmutableArray<string> taskListDescriptors,
        LspDiagnostic[] csharpTaskItems,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetTaskListDiagnosticsAsync(context, taskListDescriptors, csharpTaskItems, cancellationToken),
            cancellationToken);

    private async ValueTask<ImmutableArray<LspDiagnostic>> GetTaskListDiagnosticsAsync(
        RemoteDocumentContext context,
        ImmutableArray<string> taskListDescriptors,
        LspDiagnostic[] csharpTaskItems,
        CancellationToken cancellationToken)
    {
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        using var diagnostics = new PooledArrayBuilder<LspDiagnostic>();
        diagnostics.AddRange(TaskListDiagnosticProvider.GetTaskListDiagnostics(codeDocument, taskListDescriptors));
        diagnostics.AddRange(_translateDiagnosticsService.MapDiagnostics(RazorLanguageKind.CSharp, csharpTaskItems, context.Snapshot, codeDocument));
        return diagnostics.ToImmutableAndClear();
    }
}
