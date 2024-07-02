﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;
using Microsoft.AspNetCore.Razor.LanguageServer.ColorPresentation;
using Microsoft.AspNetCore.Razor.LanguageServer.Debugging;
using Microsoft.AspNetCore.Razor.LanguageServer.Definition;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentColor;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentHighlighting;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentSymbols;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.FindAllReferences;
using Microsoft.AspNetCore.Razor.LanguageServer.Folding;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.LanguageServer.Implementation;
using Microsoft.AspNetCore.Razor.LanguageServer.InlayHints;
using Microsoft.AspNetCore.Razor.LanguageServer.LinkedEditingRange;
using Microsoft.AspNetCore.Razor.LanguageServer.MapCode;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectContexts;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Refactoring;
using Microsoft.AspNetCore.Razor.LanguageServer.SignatureHelp;
using Microsoft.AspNetCore.Razor.LanguageServer.WrapWithTag;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.FoldingRanges;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using StreamJsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal partial class RazorLanguageServer : NewtonsoftLanguageServer<RazorRequestContext>, IDisposable
{
    private readonly JsonRpc _jsonRpc;
    private readonly ILoggerFactory _loggerFactory;
    private readonly LanguageServerFeatureOptions? _featureOptions;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly RazorLSPOptions _lspOptions;
    private readonly ILspServerActivationTracker? _lspServerActivationTracker;
    private readonly ITelemetryReporter _telemetryReporter;
    private readonly ClientConnection _clientConnection;

    private AbstractHandlerProvider? _handlerProvider;

    public RazorLanguageServer(
        JsonRpc jsonRpc,
        JsonSerializer serializer,
        ILoggerFactory loggerFactory,
        LanguageServerFeatureOptions? featureOptions,
        Action<IServiceCollection>? configureServices,
        RazorLSPOptions? lspOptions,
        ILspServerActivationTracker? lspServerActivationTracker,
        ITelemetryReporter telemetryReporter)
        : base(jsonRpc, serializer, CreateILspLogger(loggerFactory, telemetryReporter))
    {
        _jsonRpc = jsonRpc;
        _loggerFactory = loggerFactory;
        _featureOptions = featureOptions;
        _configureServices = configureServices;
        _lspOptions = lspOptions ?? RazorLSPOptions.Default;
        _lspServerActivationTracker = lspServerActivationTracker;
        _telemetryReporter = telemetryReporter;

        _clientConnection = new ClientConnection(_jsonRpc);

        Initialize();
    }

    public void Dispose()
    {
        _jsonRpc.Dispose();
    }

    private static ILspLogger CreateILspLogger(ILoggerFactory loggerFactory, ITelemetryReporter telemetryReporter)
    {
        return new ClaspLoggingBridge(loggerFactory, telemetryReporter);
    }

    // We override this to ensure that base.HandlerProvider is only called once.
    // CLaSP currently does not cache the result of this property, though it probably should.
    protected override AbstractHandlerProvider HandlerProvider
        => _handlerProvider ??= base.HandlerProvider;

    protected override IRequestExecutionQueue<RazorRequestContext> ConstructRequestExecutionQueue()
    {
        var handlerProvider = HandlerProvider;
        var queue = new RazorRequestExecutionQueue(this, Logger, handlerProvider);
        queue.Start();

        return queue;
    }

    protected override ILspServices ConstructLspServices()
    {
        var services = new ServiceCollection();

        var loggerFactoryWrapper = new LoggerFactoryWrapper(_loggerFactory);
        // Wrap the logger factory so that we can add [LSP] to the start of all the categories
        services.AddSingleton<ILoggerFactory>(loggerFactoryWrapper);

        if (_configureServices is not null)
        {
            _configureServices(services);
        }

        services.AddSingleton<IClientConnection>(_clientConnection);

        // Add the logger as a service in case anything in CLaSP pulls it out to do logging
        services.AddSingleton<ILspLogger>(Logger);

        services.AddSingleton<IAdhocWorkspaceFactory, AdhocWorkspaceFactory>();
        services.AddSingleton<IWorkspaceProvider, LspWorkspaceProvider>();

        var featureOptions = _featureOptions ?? new DefaultLanguageServerFeatureOptions();
        services.AddSingleton(featureOptions);

        services.AddSingleton<IFilePathService, LSPFilePathService>();

        services.AddLifeCycleServices(this, _clientConnection, _lspServerActivationTracker);

        services.AddDiagnosticServices();
        services.AddSemanticTokensServices(featureOptions);
        services.AddDocumentManagementServices(featureOptions);
        services.AddCompletionServices();
        services.AddFormattingServices();
        services.AddCodeActionsServices();
        services.AddOptionsServices(_lspOptions);
        services.AddHoverServices();
        services.AddTextDocumentServices(featureOptions);

        // Auto insert
        services.AddSingleton<IOnAutoInsertProvider, CloseTextTagOnAutoInsertProvider>();
        services.AddSingleton<IOnAutoInsertProvider, AutoClosingTagOnAutoInsertProvider>();

        if (!featureOptions.UseRazorCohostServer)
        {
            // Folding Range Providers
            services.AddSingleton<IRazorFoldingRangeProvider, RazorCodeBlockFoldingProvider>();
            services.AddSingleton<IRazorFoldingRangeProvider, RazorCSharpStatementFoldingProvider>();
            services.AddSingleton<IRazorFoldingRangeProvider, RazorCSharpStatementKeywordFoldingProvider>();
            services.AddSingleton<IRazorFoldingRangeProvider, SectionDirectiveFoldingProvider>();
            services.AddSingleton<IRazorFoldingRangeProvider, UsingsFoldingRangeProvider>();

            services.AddSingleton<IFoldingRangeService, FoldingRangeService>();
        }

        // Other
        services.AddSingleton<RazorComponentSearchEngine, DefaultRazorComponentSearchEngine>();

        // Get the DefaultSession for telemetry. This is set by VS with
        // TelemetryService.SetDefaultSession and provides the correct
        // appinsights keys etc
        services.AddSingleton<ITelemetryReporter>(_telemetryReporter);

        // Defaults: For when the caller hasn't provided them through the `configure` action.
        services.TryAddSingleton<IHostServicesProvider, DefaultHostServicesProvider>();

        AddHandlers(services, featureOptions);

        var lspServices = new LspServices(services);

        return lspServices;

        static void AddHandlers(IServiceCollection services, LanguageServerFeatureOptions featureOptions)
        {
            // Not calling AddHandler because we want to register this endpoint as an IOnInitialized too
            services.AddSingleton<RazorConfigurationEndpoint>();
            services.AddSingleton<IMethodHandler, RazorConfigurationEndpoint>(s => s.GetRequiredService<RazorConfigurationEndpoint>());
            // Transient because it should only be used once and I'm hoping it doesn't stick around.
            services.AddTransient<IOnInitialized>(sp => sp.GetRequiredService<RazorConfigurationEndpoint>());

            services.AddHandlerWithCapabilities<ImplementationEndpoint>();
            services.AddHandlerWithCapabilities<SignatureHelpEndpoint>();
            services.AddHandlerWithCapabilities<DocumentHighlightEndpoint>();
            services.AddHandlerWithCapabilities<OnAutoInsertEndpoint>();

            services.AddHandlerWithCapabilities<RenameEndpoint>();
            services.AddHandlerWithCapabilities<DefinitionEndpoint>();

            if (!featureOptions.UseRazorCohostServer)
            {
                services.AddHandlerWithCapabilities<LinkedEditingRangeEndpoint>();
                services.AddHandlerWithCapabilities<FoldingRangeEndpoint>();
            }

            services.AddHandler<WrapWithTagEndpoint>();
            services.AddHandler<RazorBreakpointSpanEndpoint>();
            services.AddHandler<RazorProximityExpressionsEndpoint>();

            services.AddHandlerWithCapabilities<DocumentColorEndpoint>();
            services.AddSingleton<IDocumentColorService, DocumentColorService>();

            services.AddHandler<ColorPresentationEndpoint>();
            services.AddHandlerWithCapabilities<ValidateBreakpointRangeEndpoint>();
            services.AddHandlerWithCapabilities<FindAllReferencesEndpoint>();
            services.AddHandlerWithCapabilities<ProjectContextsEndpoint>();
            services.AddHandlerWithCapabilities<DocumentSymbolEndpoint>();
            services.AddHandlerWithCapabilities<MapCodeEndpoint>();

            services.AddSingleton<IInlayHintService, InlayHintService>();

            services.AddHandlerWithCapabilities<InlayHintEndpoint>();
            services.AddHandler<InlayHintResolveEndpoint>();
        }
    }
}
