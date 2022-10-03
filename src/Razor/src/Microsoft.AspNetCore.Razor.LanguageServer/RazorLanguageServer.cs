// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Telemetry;
using Microsoft.AspNetCore.Razor.LanguageServer.Debugging;
using Microsoft.AspNetCore.Razor.LanguageServer.Definition;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentColor;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Folding;
using Microsoft.AspNetCore.Razor.LanguageServer.LinkedEditingRange;
using Microsoft.AspNetCore.Razor.LanguageServer.Refactoring;
using Microsoft.AspNetCore.Razor.LanguageServer.Telemetry;
using Microsoft.AspNetCore.Razor.LanguageServer.WrapWithTag;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Telemetry;
using StreamJsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class VerifyingHandlerProvider : IHandlerProvider
{
    private readonly IHandlerProvider _baseProvider;
    private bool _verified = false;

    public VerifyingHandlerProvider(IHandlerProvider baseProvider)
    {
        _baseProvider = baseProvider;
    }

    public IMethodHandler GetMethodHandler(string method, Type? requestType, Type? responseType)
    {
        if (!_verified)
        {
            var registeredMethods = _baseProvider.GetRegisteredMethods();
            var handlerTypes = this.GetType().Assembly.GetTypes()
                .Where(t => typeof(IMethodHandler).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

            if (registeredMethods.Length != handlerTypes.Count())
            {
                var unregisteredHandlers = handlerTypes.Where(t => !registeredMethods.Any(m => m.MethodName == GetMethodFromType(t)));
                throw new InvalidOperationException($"Unregistered handlers: {string.Join(";", unregisteredHandlers.Select(t => t.Name))}");
            }
        }

        return _baseProvider.GetMethodHandler(method, requestType, responseType);

        static string GetMethodFromType(Type t)
        {
            var attribute = t.GetCustomAttribute<LanguageServerEndpointAttribute>();
            if (attribute is null)
            {
                foreach(var inter in t.GetInterfaces())
                {
                    attribute = inter.GetCustomAttribute<LanguageServerEndpointAttribute>();

                    if (attribute is not null)
                    {
                        break;
                    }
                }
            }

            if (attribute is null)
            {
                throw new NotImplementedException();
            }

            return attribute.Method;
        }
    }

    public ImmutableArray<RequestHandlerMetadata> GetRegisteredMethods()
    {
        return _baseProvider.GetRegisteredMethods();
    }
}

internal class RazorLanguageServer : AbstractLanguageServer<RazorRequestContext>
{
    private readonly JsonRpc _jsonRpc;
    private readonly LanguageServerFeatureOptions? _featureOptions;
    private readonly ProjectSnapshotManagerDispatcher? _projectSnapshotManagerDispatcher;
    private readonly Action<IServiceCollection>? _configureServer;

    public RazorLanguageServer(
        JsonRpc jsonRpc,
        ILspLogger logger,
        ProjectSnapshotManagerDispatcher? projectSnapshotManagerDispatcher,
        LanguageServerFeatureOptions? featureOptions,
        Action<IServiceCollection>? configureServer)
        : base(jsonRpc, logger)
    {
        _jsonRpc = jsonRpc;
        _featureOptions = featureOptions;
        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        _configureServer = configureServer;

        Initialize();
    }

    protected override IHandlerProvider GetHandlerProvider()
    {
        IHandlerProvider provider;
#if DEBUG
        var baseProvider = base.GetHandlerProvider();
        provider = new VerifyingHandlerProvider(baseProvider);
#else
        provider = base.GetHandlerProvider();
#endif

        return provider;
    }

    protected override ILspServices ConstructLspServices()
    {
        var services = new ServiceCollection()
            .AddOptions()
            .AddLogging();

        if (_configureServer is not null)
        {
            _configureServer(services);
        }

        var serverManager = new DefaultClientNotifierService(_jsonRpc);
        services.AddSingleton<ClientNotifierServiceBase>(serverManager);
        if (_logger is LspLogger lspLogger)
        {
            lspLogger.Initialize(serverManager);
        }

        services.AddSingleton<ILspLogger>(_logger);
        if (_logger is ILogger ilogger)
        {
            services.AddSingleton<ILogger>(ilogger);
        }

        services.AddSingleton<ErrorReporter, LanguageServerErrorReporter>();

        if (_projectSnapshotManagerDispatcher is null)
        {
            services.AddSingleton<ProjectSnapshotManagerDispatcher, LSPProjectSnapshotManagerDispatcher>();
        }
        else
        {
            services.AddSingleton<ProjectSnapshotManagerDispatcher>(_projectSnapshotManagerDispatcher);
        }

        services.AddSingleton<AdhocWorkspaceFactory, DefaultAdhocWorkspaceFactory>();

        var featureOptions = _featureOptions ?? new DefaultLanguageServerFeatureOptions();
        services.AddSingleton(featureOptions);

        services.AddLifeCycleServices(this, serverManager);

        services.AddSemanticTokensServices();
        services.AddDocumentManagmentServices();
        services.AddCompletionServices(featureOptions);
        services.AddFormattingServices();
        services.AddCodeActionsServices();
        services.AddOptionsServices();
        services.AddHoverServices();
        services.AddTextDocumentServices();

        // Auto insert
        services.AddSingleton<RazorOnAutoInsertProvider, CloseTextTagOnAutoInsertProvider>();
        services.AddSingleton<RazorOnAutoInsertProvider, AutoClosingTagOnAutoInsertProvider>();

        // Folding Range Providers
        services.AddSingleton<RazorFoldingRangeProvider, RazorCodeBlockFoldingProvider>();

        // Other
        services.AddSingleton<HtmlFactsService, DefaultHtmlFactsService>();
        services.AddSingleton<WorkspaceDirectoryPathResolver, DefaultWorkspaceDirectoryPathResolver>();
        services.AddSingleton<RazorComponentSearchEngine, DefaultRazorComponentSearchEngine>();

        // Folding Range Providers
        services.AddSingleton<RazorFoldingRangeProvider, RazorCodeBlockFoldingProvider>();

        // Get the DefaultSession for telemetry. This is set by VS with
        // TelemetryService.SetDefaultSession and provides the correct
        // appinsights keys etc
        services.AddSingleton<ITelemetryReporter>(provider =>
            new TelemetryReporter(ImmutableArray.Create(TelemetryService.DefaultSession), provider.GetRequiredService<ILoggerFactory>()));

        // Defaults: For when the caller hasn't provided them through the `configure` action.
        services.TryAddSingleton<HostServicesProvider, DefaultHostServicesProvider>();

        AddHandlers(services);

        var lspServices = new LspServices(services);

        return lspServices;

        static void AddHandlers(IServiceCollection services)
        {
            services.AddHandler<RazorDiagnosticsEndpoint>();
            services.AddHandler<RazorConfigurationEndpoint>();
            services.AddRegisteringHandler<OnAutoInsertEndpoint>();
            services.AddHandler<MonitorProjectConfigurationFilePathEndpoint>();
            services.AddRegisteringHandler<RenameEndpoint>();
            services.AddRegisteringHandler<RazorDefinitionEndpoint>();
            services.AddRegisteringHandler<LinkedEditingRangeEndpoint>();
            services.AddHandler<WrapWithTagEndpoint>();
            services.AddHandler<RazorBreakpointSpanEndpoint>();
            services.AddHandler<RazorProximityExpressionsEndpoint>();
            services.AddRegisteringHandler<DocumentColorEndpoint>();
            services.AddRegisteringHandler<FoldingRangeEndpoint>();
            services.AddRegisteringHandler<ValidateBreakpointRangeEndpoint>();
        }
    }

    internal T GetRequiredService<T>() where T : notnull
    {
        var lspServices = GetLspServices();

        return lspServices.GetRequiredService<T>();
    }
}
