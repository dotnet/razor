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
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.Extensions.Options;
using Microsoft.CommonLanguageServerProtocol.Framework;
using System;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.AspNetCore.Razor.LanguageServer.Hover;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CommonLanguageServerProtocol.Framework.Handlers;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation;
using StreamJsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

internal static class IServiceCollectionExtensions
{
    public static void AddLifeCycleServices(this IServiceCollection services, RazorLanguageServer razorLanguageServer, JsonRpc jsonRpc)
    {
        services.AddHandler<RazorInitializeEndpoint>();
        services.AddHandler<RazorInitializedEndpoint>();
        services.AddHandler<ExitHandler<RazorRequestContext>>();
        services.AddHandler<ShutdownHandler<RazorRequestContext>>();

        services.AddSingleton<ILifeCycleManager>(razorLanguageServer);
        services.AddSingleton<IInitializeManager<InitializeParams, InitializeResult>, CapabilitiesManager>();
        services.AddSingleton<IRequestContextFactory<RazorRequestContext>, RazorRequestContextFactory>();

        var serverManager = new DefaultClientNotifierService(jsonRpc);
        services.AddSingleton<ClientNotifierServiceBase>(serverManager);
        services.AddSingleton<IOnInitialized>(serverManager);
    }

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

        services.AddRegisteringHandler<RazorDocumentFormattingEndpoint>();
        services.AddRegisteringHandler<RazorDocumentOnTypeFormattingEndpoint>();
        services.AddRegisteringHandler<RazorDocumentRangeFormattingEndpoint>();
    }

    public static void AddCompletionServices(this IServiceCollection services, LanguageServerFeatureOptions featureOptions)
    {
        services.AddRegisteringHandler<InlineCompletionEndpoint>();

        if (featureOptions.SingleServerCompletionSupport)
        {
            services.AddRegisteringHandler<RazorCompletionEndpoint>();
            services.AddRegisteringHandler<RazorCompletionResolveEndpoint>();
        }
        else
        {
            services.AddRegisteringHandler<LegacyRazorCompletionEndpoint>();
            services.AddHandler<LegacyRazorCompletionResolveEndpoint>();
        }

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

    public static void AddHoverServices(this IServiceCollection services)
    {
        services.AddRegisteringHandler<RazorHoverEndpoint>();

        services.AddSingleton<RazorHoverInfoService, DefaultRazorHoverInfoService>();
    }

    public static void AddSemanticTokensServices(this IServiceCollection services)
    {
        services.AddRegisteringHandler<RazorSemanticTokensEndpoint>();
        services.AddRegisteringHandler<SemanticTokensRefreshEndpoint>();

        services.AddSingleton<WorkspaceSemanticTokensRefreshPublisher, DefaultWorkspaceSemanticTokensRefreshPublisher>();
        services.AddSingleton<ProjectSnapshotChangeTrigger, DefaultWorkspaceSemanticTokensRefreshTrigger>();
        services.AddSingleton<RazorSemanticTokensInfoService, DefaultRazorSemanticTokensInfoService>();
    }

    public static void AddCodeActionsServices(this IServiceCollection services)
    {
        services.AddRegisteringHandler<CodeActionEndpoint>();
        services.AddHandler<CodeActionResolutionEndpoint>();

        // CSharp Code actions
        services.AddSingleton<CSharpCodeActionProvider, TypeAccessibilityCodeActionProvider>();
        services.AddSingleton<CSharpCodeActionProvider, DefaultCSharpCodeActionProvider>();
        services.AddSingleton<CSharpCodeActionResolver, DefaultCSharpCodeActionResolver>();
        services.AddSingleton<CSharpCodeActionResolver, AddUsingsCSharpCodeActionResolver>();
        services.AddSingleton<CSharpCodeActionResolver, UnformattedRemappingCSharpCodeActionResolver>();

        // Razor Code actions
        services.AddSingleton<RazorCodeActionProvider, ExtractToCodeBehindCodeActionProvider>();
        services.AddSingleton<RazorCodeActionResolver, ExtractToCodeBehindCodeActionResolver>();
        services.AddSingleton<RazorCodeActionProvider, ComponentAccessibilityCodeActionProvider>();
        services.AddSingleton<RazorCodeActionResolver, CreateComponentCodeActionResolver>();
        services.AddSingleton<RazorCodeActionResolver, AddUsingsCodeActionResolver>();
    }

    public static void AddTextDocumentServices(this IServiceCollection services)
    {
        services.AddRegisteringHandler<TextDocumentTextPresentationEndpoint>();
        services.AddRegisteringHandler<TextDocumentUriPresentationEndpoint>();

        services.AddRegisteringHandler<RazorDidChangeTextDocumentEndpoint>();
        services.AddHandler<RazorDidCloseTextDocumentEndpoint>();
        services.AddHandler<RazorDidOpenTextDocumentEndpoint>();

        services.AddHandler<RazorMapToDocumentRangesEndpoint>();
    }

    public static void AddOptionsServices(this IServiceCollection services)
    {
        services.AddSingleton<RazorConfigurationService, DefaultRazorConfigurationService>();
        services.AddSingleton<RazorLSPOptionsMonitor>();
        services.AddSingleton<IOptionsMonitor<RazorLSPOptions>, RazorLSPOptionsMonitor>();
    }

    public static void AddDocumentManagmentServices(this IServiceCollection services)
    {
        services.AddSingleton<GeneratedDocumentPublisher, DefaultGeneratedDocumentPublisher>();
        services.AddSingleton<ProjectSnapshotChangeTrigger>((services) => services.GetRequiredService<GeneratedDocumentPublisher>());
        services.AddSingleton<DocumentContextFactory, DefaultDocumentContextFactory>();
        services.AddSingleton<FilePathNormalizer>();

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

    public static void AddRegisteringHandler<T>(this IServiceCollection services)
        where T : class, IMethodHandler, IRegistrationExtension
    {
        services.AddSingleton<T>();
        services.AddSingleton<IMethodHandler, T>(s => s.GetRequiredService<T>());
        // Transient because it should only be used once and I'm hoping it doesn't stick around.
        services.AddTransient<IRegistrationExtension, T>(s => s.GetRequiredService<T>());
    }

    public static void AddHandler<T>(this IServiceCollection services)
        where T : class, IMethodHandler
    {
        if (typeof(IRegistrationExtension).IsAssignableFrom(typeof(T)))
        {
            throw new NotImplementedException($"{nameof(T)} is not using {nameof(AddRegisteringHandler)} when it implements {nameof(IRegistrationExtension)}");
        }

        services.AddSingleton<IMethodHandler, T>();
    }
}
