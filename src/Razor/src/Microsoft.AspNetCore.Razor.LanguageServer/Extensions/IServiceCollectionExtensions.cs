﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentSynchronization;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.LanguageServer.Hover;
using Microsoft.AspNetCore.Razor.LanguageServer.InlineCompletion;
using Microsoft.AspNetCore.Razor.LanguageServer.Mapping;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.SpellCheck;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

internal static class IServiceCollectionExtensions
{
    public static void AddLifeCycleServices(this IServiceCollection services, RazorLanguageServer razorLanguageServer, ClientConnection serverManager, ILspServerActivationTracker? lspServerActivationTracker)
    {
        services.AddHandler<RazorInitializeEndpoint>();
        services.AddHandler<RazorInitializedEndpoint>();

        var razorLifeCycleManager = new RazorLifeCycleManager(razorLanguageServer, lspServerActivationTracker);
        services.AddSingleton<ILifeCycleManager>(razorLifeCycleManager);
        services.AddSingleton<RazorLifeCycleManager>(razorLifeCycleManager);
        services.AddSingleton<CapabilitiesManager>();
        services.AddSingleton<IInitializeManager<InitializeParams, InitializeResult>, CapabilitiesManager>(sp => sp.GetRequiredService<CapabilitiesManager>());
        services.AddSingleton<IClientCapabilitiesService>(sp => sp.GetRequiredService<CapabilitiesManager>());
        services.AddSingleton<AbstractRequestContextFactory<RazorRequestContext>, RazorRequestContextFactory>();

        services.AddSingleton<ICapabilitiesProvider, RazorLanguageServerCapability>();

        services.AddSingleton<IOnInitialized>(serverManager);
    }

    public static void AddFormattingServices(this IServiceCollection services)
    {
        // Formatting
        services.AddSingleton<IRazorFormattingService, RazorFormattingService>();

        // Formatting Passes
        services.AddSingleton<IFormattingPass, HtmlFormattingPass>();
        services.AddSingleton<IFormattingPass, CSharpFormattingPass>();
        services.AddSingleton<IFormattingPass, CSharpOnTypeFormattingPass>();
        services.AddSingleton<IFormattingPass, FormattingDiagnosticValidationPass>();
        services.AddSingleton<IFormattingPass, FormattingContentValidationPass>();
        services.AddSingleton<IFormattingPass, RazorFormattingPass>();

        services.AddHandlerWithCapabilities<DocumentFormattingEndpoint>();
        services.AddHandlerWithCapabilities<DocumentOnTypeFormattingEndpoint>();
        services.AddHandlerWithCapabilities<DocumentRangeFormattingEndpoint>();
    }

    public static void AddCompletionServices(this IServiceCollection services, LanguageServerFeatureOptions featureOptions)
    {
        services.AddHandlerWithCapabilities<InlineCompletionEndpoint>();

        if (featureOptions.SingleServerCompletionSupport)
        {
            services.AddHandlerWithCapabilities<RazorCompletionEndpoint>();
            services.AddHandlerWithCapabilities<RazorCompletionResolveEndpoint>();
        }
        else
        {
            services.AddHandlerWithCapabilities<LegacyRazorCompletionEndpoint>();
            services.AddHandlerWithCapabilities<LegacyRazorCompletionResolveEndpoint>();
        }

        services.AddSingleton<CompletionListCache>();
        services.AddSingleton<CompletionListProvider>();
        services.AddSingleton<DelegatedCompletionListProvider>();
        services.AddSingleton<RazorCompletionListProvider>();
        services.AddSingleton<DelegatedCompletionResponseRewriter, TextEditResponseRewriter>();
        services.AddSingleton<DelegatedCompletionResponseRewriter, DesignTimeHelperResponseRewriter>();
        services.AddSingleton<DelegatedCompletionResponseRewriter, HtmlCommitCharacterResponseRewriter>();
        services.AddSingleton<DelegatedCompletionResponseRewriter, SnippetResponseRewriter>();

        services.AddSingleton<AggregateCompletionItemResolver>();
        services.AddSingleton<CompletionItemResolver, RazorCompletionItemResolver>();
        services.AddSingleton<CompletionItemResolver, DelegatedCompletionItemResolver>();
        services.AddSingleton<ITagHelperCompletionService, LspTagHelperCompletionService>();
        services.AddSingleton<IRazorCompletionFactsService, LspRazorCompletionFactsService>();
        services.AddSingleton<IRazorCompletionItemProvider, DirectiveCompletionItemProvider>();
        services.AddSingleton<IRazorCompletionItemProvider, DirectiveAttributeCompletionItemProvider>();
        services.AddSingleton<IRazorCompletionItemProvider, DirectiveAttributeParameterCompletionItemProvider>();
        services.AddSingleton<IRazorCompletionItemProvider, DirectiveAttributeTransitionCompletionItemProvider>();
        services.AddSingleton<IRazorCompletionItemProvider, MarkupTransitionCompletionItemProvider>();
        services.AddSingleton<IRazorCompletionItemProvider, TagHelperCompletionProvider>();
    }

    public static void AddDiagnosticServices(this IServiceCollection services)
    {
        services.AddHandlerWithCapabilities<DocumentPullDiagnosticsEndpoint>();
        services.AddHandler<WorkspacePullDiagnosticsEndpoint>();
        services.AddSingleton<RazorTranslateDiagnosticsService>();
        services.AddSingleton(sp => new Lazy<RazorTranslateDiagnosticsService>(sp.GetRequiredService<RazorTranslateDiagnosticsService>));
    }

    public static void AddHoverServices(this IServiceCollection services)
    {
        services.AddHandlerWithCapabilities<HoverEndpoint>();

        services.AddSingleton<IHoverService, HoverService>();
    }

    public static void AddSemanticTokensServices(this IServiceCollection services, LanguageServerFeatureOptions featureOptions)
    {
        if (!featureOptions.UseRazorCohostServer)
        {
            services.AddHandlerWithCapabilities<SemanticTokensRangeEndpoint>();
            // Ensure that we don't add the default service if something else has added one.
            services.TryAddSingleton<IRazorSemanticTokensInfoService, RazorSemanticTokensInfoService>();
            services.AddSingleton<ICSharpSemanticTokensProvider, LSPCSharpSemanticTokensProvider>();

            services.AddSingleton<ISemanticTokensLegendService, RazorSemanticTokensLegendService>();
        }

        services.AddHandler<RazorSemanticTokensRefreshEndpoint>();

        services.AddSingleton<IWorkspaceSemanticTokensRefreshNotifier, WorkspaceSemanticTokensRefreshNotifier>();
        services.AddSingleton<IRazorStartupService, WorkspaceSemanticTokensRefreshTrigger>();
    }

    public static void AddCodeActionsServices(this IServiceCollection services)
    {
        services.AddHandlerWithCapabilities<CodeActionEndpoint>();
        services.AddHandler<CodeActionResolveEndpoint>();

        // CSharp Code actions
        services.AddSingleton<ICSharpCodeActionProvider, TypeAccessibilityCodeActionProvider>();
        services.AddSingleton<ICSharpCodeActionProvider, DefaultCSharpCodeActionProvider>();
        services.AddSingleton<CSharpCodeActionResolver, DefaultCSharpCodeActionResolver>();
        services.AddSingleton<CSharpCodeActionResolver, UnformattedRemappingCSharpCodeActionResolver>();

        // Razor Code actions
        services.AddSingleton<IRazorCodeActionProvider, ExtractToCodeBehindCodeActionProvider>();
        services.AddSingleton<IRazorCodeActionResolver, ExtractToCodeBehindCodeActionResolver>();
        services.AddSingleton<IRazorCodeActionProvider, ComponentAccessibilityCodeActionProvider>();
        services.AddSingleton<IRazorCodeActionResolver, CreateComponentCodeActionResolver>();
        services.AddSingleton<IRazorCodeActionResolver, AddUsingsCodeActionResolver>();
        services.AddSingleton<IRazorCodeActionProvider, GenerateMethodCodeActionProvider>();
        services.AddSingleton<IRazorCodeActionResolver, GenerateMethodCodeActionResolver>();

        // Html Code actions
        services.AddSingleton<IHtmlCodeActionProvider, DefaultHtmlCodeActionProvider>();
        services.AddSingleton<HtmlCodeActionResolver, DefaultHtmlCodeActionResolver>();
    }

    public static void AddTextDocumentServices(this IServiceCollection services)
    {
        services.AddHandlerWithCapabilities<TextDocumentTextPresentationEndpoint>();
        services.AddHandlerWithCapabilities<TextDocumentUriPresentationEndpoint>();

        services.AddHandlerWithCapabilities<DocumentSpellCheckEndpoint>();
        services.AddHandler<WorkspaceSpellCheckEndpoint>();

        services.AddHandlerWithCapabilities<DocumentDidChangeEndpoint>();
        services.AddHandler<DocumentDidCloseEndpoint>();
        services.AddHandler<DocumentDidOpenEndpoint>();
        services.AddHandler<DocumentDidSaveEndpoint>();

        services.AddHandler<RazorMapToDocumentRangesEndpoint>();
        services.AddHandler<RazorLanguageQueryEndpoint>();
    }

    public static void AddOptionsServices(this IServiceCollection services, RazorLSPOptions currentOptions)
    {
        services.AddSingleton<IConfigurationSyncService, DefaultRazorConfigurationService>();
        services.AddSingleton(s =>
        {
            return new RazorLSPOptionsMonitor(
                s.GetRequiredService<IConfigurationSyncService>(),
                currentOptions);
        });
    }

    public static void AddDocumentManagementServices(this IServiceCollection services, LanguageServerFeatureOptions featureOptions)
    {
        services.AddSingleton<IGeneratedDocumentPublisher, GeneratedDocumentPublisher>();
        services.AddSingleton<IRazorStartupService>((services) => (GeneratedDocumentPublisher)services.GetRequiredService<IGeneratedDocumentPublisher>());
        services.AddSingleton<IDocumentContextFactory, DocumentContextFactory>();
        services.AddSingleton(sp => new Lazy<IDocumentContextFactory>(sp.GetRequiredService<IDocumentContextFactory>));

        services.AddSingleton<IDocumentVersionCache, DocumentVersionCache>();
        services.AddSingleton((services) => (IRazorStartupService)services.GetRequiredService<IDocumentVersionCache>());

        services.AddSingleton<RemoteTextLoaderFactory, DefaultRemoteTextLoaderFactory>();
        services.AddSingleton<ISnapshotResolver, SnapshotResolver>();
        services.AddSingleton<IRazorProjectService, RazorProjectService>();
        services.AddSingleton<IRazorStartupService, OpenDocumentGenerator>();
        services.AddSingleton<IRazorDocumentMappingService, RazorDocumentMappingService>();
        services.AddSingleton<RazorFileChangeDetectorManager>();

        if (featureOptions.UseProjectConfigurationEndpoint)
        {
            services.AddSingleton<ProjectConfigurationStateManager>();
        }
        else 
        {
            services.AddSingleton<IProjectConfigurationFileChangeListener, ProjectConfigurationStateSynchronizer>();
        }

        services.AddSingleton<IRazorFileChangeListener, RazorFileSynchronizer>();

        // If we're not monitoring the whole workspace folder for configuration changes, then we don't actually need the the file change
        // detector wired up via DI, as the razor/monitorProjectConfigurationFilePath endpoint will directly construct one. This means
        // it can be a little simpler, and doesn't need to worry about which folders it's told to listen to.
        if (featureOptions.MonitorWorkspaceFolderForConfigurationFiles)
        {
            services.AddSingleton<IFileChangeDetector, ProjectConfigurationFileChangeDetector>();
        }

        services.AddSingleton<IFileChangeDetector, RazorFileChangeDetector>();

        // Document processed listeners
        if (!featureOptions.SingleServerSupport)
        {
            // If single server is on, then we don't want to publish diagnostics, so best to just not hook up to any
            // events etc.
            services.AddSingleton<IDocumentProcessedListener, RazorDiagnosticsPublisher>();
        }

        services.AddSingleton<IDocumentProcessedListener, GeneratedDocumentSynchronizer>();
        services.AddSingleton<IDocumentProcessedListener, CodeDocumentReferenceHolder>();

        services.AddSingleton<LSPTagHelperTooltipFactory, DefaultLSPTagHelperTooltipFactory>();
        services.AddSingleton<VSLSPTagHelperTooltipFactory, DefaultVSLSPTagHelperTooltipFactory>();

        // Add project snapshot manager
        services.AddSingleton<IProjectEngineFactoryProvider, LspProjectEngineFactoryProvider>();
        services.AddSingleton<IProjectSnapshotManager, ProjectSnapshotManager>();
    }

    public static void AddHandlerWithCapabilities<T>(this IServiceCollection services)
        where T : class, IMethodHandler, ICapabilitiesProvider
    {
        services.AddSingleton<T>();
        services.AddSingleton<IMethodHandler, T>(s => s.GetRequiredService<T>());
        // Transient because it should only be used once and I'm hoping it doesn't stick around.
        services.AddTransient<ICapabilitiesProvider, T>(s => s.GetRequiredService<T>());
    }

    public static void AddHandler<T>(this IServiceCollection services)
        where T : class, IMethodHandler
    {
        if (typeof(ICapabilitiesProvider).IsAssignableFrom(typeof(T)))
        {
            throw new NotImplementedException($"{nameof(T)} is not using {nameof(AddHandlerWithCapabilities)} when it implements {nameof(ICapabilitiesProvider)}");
        }

        services.AddSingleton<IMethodHandler, T>();
    }
}
