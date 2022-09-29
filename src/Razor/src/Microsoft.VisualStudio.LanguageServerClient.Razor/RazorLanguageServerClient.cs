﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Nerdbank.Streams;
using StreamJsonRpc;
using Task = System.Threading.Tasks.Task;
using Trace = Microsoft.AspNetCore.Razor.LanguageServer.Trace;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(ILanguageClient))]
    [ContentType(RazorConstants.RazorLSPContentTypeName)]
    internal class RazorLanguageServerClient : ILanguageClient, ILanguageClientCustomMessage2, ILanguageClientPriority
    {
        private const string LogFileIdentifier = "Razor.RazorLanguageServerClient";

        private readonly RazorLanguageServerCustomMessageTarget _customMessageTarget;
        private readonly ILanguageClientMiddleLayer _middleLayer;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly ProjectConfigurationFilePathStore _projectConfigurationFilePathStore;
        private readonly RazorLanguageServerLogHubLoggerProviderFactory _logHubLoggerProviderFactory;
        private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
        private readonly VisualStudioHostServicesProvider? _vsHostWorkspaceServicesProvider;
        private readonly object _shutdownLock;
        private RazorLanguageServerWrapper? _server;
        private IDisposable? _serverShutdownDisposable;
        private LogHubLoggerProvider? _loggerProvider;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;

        private const string RazorLSPLogLevel = "RAZOR_TRACE";

        public event AsyncEventHandler<EventArgs>? StartAsync;
        public event AsyncEventHandler<EventArgs>? StopAsync
        {
            add { }
            remove { }
        }

        [ImportingConstructor]
        public RazorLanguageServerClient(
            RazorLanguageServerCustomMessageTarget customTarget,
            RazorLanguageClientMiddleLayer middleLayer,
            LSPRequestInvoker requestInvoker,
            ProjectConfigurationFilePathStore projectConfigurationFilePathStore,
            RazorLanguageServerLogHubLoggerProviderFactory logHubLoggerProviderFactory,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            [Import(AllowDefault = true)] VisualStudioHostServicesProvider? vsHostWorkspaceServicesProvider)
        {
            if (customTarget is null)
            {
                throw new ArgumentNullException(nameof(customTarget));
            }

            if (middleLayer is null)
            {
                throw new ArgumentNullException(nameof(middleLayer));
            }

            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            if (projectConfigurationFilePathStore is null)
            {
                throw new ArgumentNullException(nameof(projectConfigurationFilePathStore));
            }

            if (logHubLoggerProviderFactory is null)
            {
                throw new ArgumentNullException(nameof(logHubLoggerProviderFactory));
            }

            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (languageServerFeatureOptions is null)
            {
                throw new ArgumentNullException(nameof(languageServerFeatureOptions));
            }

            _customMessageTarget = customTarget;
            _middleLayer = middleLayer;
            _requestInvoker = requestInvoker;
            _projectConfigurationFilePathStore = projectConfigurationFilePathStore;
            _logHubLoggerProviderFactory = logHubLoggerProviderFactory;
            _languageServerFeatureOptions = languageServerFeatureOptions;
            _vsHostWorkspaceServicesProvider = vsHostWorkspaceServicesProvider;
            _shutdownLock = new object();
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        }

        public string Name => RazorLSPConstants.RazorLanguageServerName;

        public IEnumerable<string>? ConfigurationSections => null;

        public object? InitializationOptions => null;

        public IEnumerable<string>? FilesToWatch => null;

        public object MiddleLayer => _middleLayer;

        public object CustomMessageTarget => _customMessageTarget;

        public bool IsOverriding => false;

        // We set a priority to ensure that our Razor language server is always chosen if there's a conflict for which language server to prefer.
        public int Priority => 10;

        public bool ShowNotificationOnInitializeFailed => true;

        public async Task<Connection?> ActivateAsync(CancellationToken token)
        {
            // Swap to background thread, nothing below needs to be done on the UI thread.
            await TaskScheduler.Default;

            var (clientStream, serverStream) = FullDuplexStream.CreatePair();

            await EnsureCleanedUpServerAsync(token).ConfigureAwait(false);

            var traceLevel = GetVerbosity();

            // Initialize Logging Infrastructure
            _loggerProvider = (LogHubLoggerProvider)await _logHubLoggerProviderFactory.GetOrCreateAsync(LogFileIdentifier, token).ConfigureAwait(false);

            var logHubLogger = _loggerProvider.CreateLogger("Razor");
            var razorLogger = new LoggerAdapter(logHubLogger);
            _server = RazorLanguageServerWrapper.Create(serverStream, serverStream, razorLogger, _projectSnapshotManagerDispatcher, ConfigureLanguageServer, _languageServerFeatureOptions);

            var connection = new Connection(clientStream, clientStream);
            return connection;
        }

        private void ConfigureLanguageServer(IServiceCollection serviceCollection)
        {
            serviceCollection.AddLogging(logging =>
            {
                logging.AddFilter<LogHubLoggerProvider>(level => true);
                logging.AddProvider(_loggerProvider);
            });

            if (_vsHostWorkspaceServicesProvider is not null)
            {
                var wrapper = new HostServicesProviderWrapper(_vsHostWorkspaceServicesProvider);
                serviceCollection.AddSingleton<HostServicesProvider>(wrapper);
            }
        }

        private Trace GetVerbosity()
        {
            var logString = Environment.GetEnvironmentVariable(RazorLSPLogLevel);
            var result = Enum.TryParse<Trace>(logString, out var parsedTrace) ? parsedTrace : Trace.Off;

            return result;
        }

        private async Task EnsureCleanedUpServerAsync(CancellationToken token)
        {
            const int WaitForShutdownAttempts = 10;

            if (_server is null)
            {
                // Server was already cleaned up
                return;
            }

            var attempts = 0;
            while (_server is not null && ++attempts < WaitForShutdownAttempts)
            {
                // Server failed to shutdown, lets wait a little bit and check again.
                await Task.Delay(100, token).ConfigureAwait(false);
            }

            if (_server is not null)
            {
                // Server still hasn't shutdown, attempt an ungraceful shutdown.
                await _server.DisposeAsync();

                ServerShutdown();
            }
        }

        private void ServerShutdown()
        {
            lock (_shutdownLock)
            {
                if (_server is null)
                {
                    // Already shutdown
                    return;
                }

                _projectConfigurationFilePathStore.Changed -= ProjectConfigurationFilePathStore_Changed;
                _serverShutdownDisposable?.Dispose();
                _serverShutdownDisposable = null;
                _server = null;
            }
        }

        private void ProjectConfigurationFilePathStore_Changed(object sender, ProjectConfigurationFilePathChangedEventArgs args)
        {
            _ = ProjectConfigurationFilePathStore_ChangedAsync(args, CancellationToken.None);
        }

        private async Task ProjectConfigurationFilePathStore_ChangedAsync(ProjectConfigurationFilePathChangedEventArgs args, CancellationToken cancellationToken)
        {
            try
            {
                var parameter = new MonitorProjectConfigurationFilePathParams()
                {
                    ProjectFilePath = args.ProjectFilePath,
                    ConfigurationFilePath = args.ConfigurationFilePath,
                };

                await _requestInvoker.ReinvokeRequestOnServerAsync<MonitorProjectConfigurationFilePathParams, object>(
                    LanguageServerConstants.RazorMonitorProjectConfigurationFilePathEndpoint,
                    RazorLSPConstants.RazorLanguageServerName,
                    parameter,
                    cancellationToken);
            }
            catch (Exception)
            {
                // We're fire and forgetting here, if the request fails we're ok with that.
                //
                // Note: When moving between solutions this can fail with a null reference exception because the underlying LSP platform's
                // JsonRpc object will be `null`. This can happen in two situations:
                //      1.  There's currently a race in the platform on shutting down/activating so we don't get the opportunity to properly detatch
                //          from the configuration file path store changed event properly.
                //          Tracked by: https://github.com/dotnet/aspnetcore/issues/23819
                //      2.  The LSP platform failed to shutdown our language server properly due to a JsonRpc timeout. There's currently a limitation in
                //          the LSP platform APIs where we don't know if the LSP platform requested shutdown but our language server never saw it. Therefore,
                //          we will null-ref until our language server client boot-logic kicks back in and re-activates resulting in the old server being
                //          being cleaned up.
            }
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc) => Task.CompletedTask;

        public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            var initializationFailureContext = new InitializationFailureContext
            {
                FailureMessage = string.Format(VS.LSClientRazor.Resources.LanguageServer_Initialization_Failed,
                    Name, initializationState.StatusMessage, initializationState.InitializationException?.ToString())
            };
            return Task.FromResult<InitializationFailureContext?>(initializationFailureContext);
        }

        public Task OnLoadedAsync()
        {
            return StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public Task OnServerInitializedAsync()
        {
            ServerStarted();

            return Task.CompletedTask;
        }

        private void ServerStarted()
        {
            _projectConfigurationFilePathStore.Changed += ProjectConfigurationFilePathStore_Changed;

            var mappings = _projectConfigurationFilePathStore.GetMappings();
            foreach (var mapping in mappings)
            {
                var args = new ProjectConfigurationFilePathChangedEventArgs(mapping.Key, mapping.Value);
                ProjectConfigurationFilePathStore_Changed(this, args);
            }
        }

        private class HostServicesProviderWrapper : HostServicesProvider
        {
            private readonly VisualStudioHostServicesProvider _vsHostServicesProvider;

            public HostServicesProviderWrapper(VisualStudioHostServicesProvider vsHostServicesProvider)
            {
                _vsHostServicesProvider = vsHostServicesProvider;
            }

            public override HostServices GetServices() => _vsHostServicesProvider.GetServices();
        }
    }
}
