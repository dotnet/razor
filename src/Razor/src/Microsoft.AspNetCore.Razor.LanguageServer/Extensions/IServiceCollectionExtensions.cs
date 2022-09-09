// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

internal static class IServiceCollectionExtensions
{
    public static void AddFormattingServices(this IServiceCollection services)
    {
        // Formatting
        services.AddSingleton<RazorFormattingService, DefaultRazorFormattingService>();

        // Formatting Passes
        services.AddSingleton<IFormattingPass, HtmlFormattingPass>();
        services.AddSingleton<IFormattingPass, CSharpFormattingPass>();
        services.AddSingleton<IFormattingPass, CSharpOnTypeFormattingPass>();
        services.AddSingleton<IFormattingPass, FormattingDiagnosticValidationPass>();
        services.AddSingleton<IFormattingPass, FormattingContentValidationPass>();
        services.AddSingleton<IFormattingPass, RazorFormattingPass>();
    }

    public static void AddCompletionServices(this IServiceCollection services)
    {
        services.AddSingleton<CompletionListCache>();
        services.AddSingleton<AggregateCompletionListProvider>();
        services.AddSingleton<CompletionListProvider, DelegatedCompletionListProvider>();
        services.AddSingleton<CompletionListProvider, RazorCompletionListProvider>();
        services.AddSingleton<DelegatedCompletionResponseRewriter, TextEditResponseRewriter>();
        services.AddSingleton<DelegatedCompletionResponseRewriter, DesignTimeHelperResponseRewriter>();

        services.AddSingleton<AggregateCompletionItemResolver>();
        services.AddSingleton<CompletionItemResolver, RazorCompletionItemResolver>();
        services.AddSingleton<CompletionItemResolver, DelegatedCompletionItemResolver>();
        services.AddSingleton<TagHelperCompletionService, LanguageServerTagHelperCompletionService>();
        services.AddSingleton<RazorCompletionFactsService, DefaultRazorCompletionFactsService>();
        services.AddSingleton<RazorCompletionItemProvider, DirectiveCompletionItemProvider>();
        services.AddSingleton<RazorCompletionItemProvider, DirectiveAttributeCompletionItemProvider>();
        services.AddSingleton<RazorCompletionItemProvider, DirectiveAttributeParameterCompletionItemProvider>();
        services.AddSingleton<RazorCompletionItemProvider, DirectiveAttributeTransitionCompletionItemProvider>();
        services.AddSingleton<RazorCompletionItemProvider, MarkupTransitionCompletionItemProvider>();
        services.AddSingleton<RazorCompletionItemProvider, TagHelperCompletionProvider>();
    }

    public static void AddDocumentManagmentServices(this IServiceCollection services)
    {
        services.AddSingleton<DocumentVersionCache, DefaultDocumentVersionCache>();
        services.AddSingleton<ProjectSnapshotChangeTrigger>((services) => services.GetRequiredService<DocumentVersionCache>());

        services.AddSingleton<RemoteTextLoaderFactory, DefaultRemoteTextLoaderFactory>();
        services.AddSingleton<ProjectResolver, DefaultProjectResolver>();
        services.AddSingleton<DocumentResolver, DefaultDocumentResolver>();
        services.AddSingleton<RazorProjectService, DefaultRazorProjectService>();
        services.AddSingleton<ProjectSnapshotChangeTrigger, OpenDocumentGenerator>();
        services.AddSingleton<RazorDocumentMappingService, DefaultRazorDocumentMappingService>();
        services.AddSingleton<RazorFileChangeDetectorManager>();

        // File change listeners
        services.AddSingleton<IProjectConfigurationFileChangeListener, ProjectConfigurationStateSynchronizer>();
        services.AddSingleton<IProjectFileChangeListener, ProjectFileSynchronizer>();
        services.AddSingleton<IRazorFileChangeListener, RazorFileSynchronizer>();

        // File Change detectors
        services.AddSingleton<IFileChangeDetector, ProjectConfigurationFileChangeDetector>();
        services.AddSingleton<IFileChangeDetector, ProjectFileChangeDetector>();
        services.AddSingleton<IFileChangeDetector, RazorFileChangeDetector>();

        // Document processed listeners
        services.AddSingleton<DocumentProcessedListener, RazorDiagnosticsPublisher>();
        services.AddSingleton<DocumentProcessedListener, GeneratedDocumentSynchronizer>();
        services.AddSingleton<DocumentProcessedListener, CodeDocumentReferenceHolder>();

        services.AddSingleton<ProjectSnapshotManagerAccessor, DefaultProjectSnapshotManagerAccessor>();
        services.AddSingleton<TagHelperFactsService, DefaultTagHelperFactsService>();
        services.AddSingleton<LSPTagHelperTooltipFactory, DefaultLSPTagHelperTooltipFactory>();
        services.AddSingleton<VSLSPTagHelperTooltipFactory, DefaultVSLSPTagHelperTooltipFactory>();
    }
}
