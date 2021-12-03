﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Nerdbank.Streams;
using StreamJsonRpc;
using Task = System.Threading.Tasks.Task;
using Trace = Microsoft.AspNetCore.Razor.LanguageServer.Trace;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(ILanguageClient))]
    [ContentType(RazorLSPConstants.RazorLSPContentTypeName)]
    internal class RazorLanguageServerClient : ILanguageClient, ILanguageClientCustomMessage2, ILanguageClientPriority
    {
        private const string LogFileIdentifier = "Razor.RazorLanguageServerClient";

        private readonly RazorLanguageServerCustomMessageTarget _customMessageTarget;
        private readonly ILanguageClientMiddleLayer _middleLayer;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly ProjectConfigurationFilePathStore _projectConfigurationFilePathStore;
        private readonly RazorLanguageServerLogHubLoggerProviderFactory _logHubLoggerProviderFactory;
        private readonly VSLanguageServerFeatureOptions _vsLanguageServerFeatureOptions;
        private readonly VSHostServicesProvider _vsHostWorkspaceServicesProvider;
        private readonly object _shutdownLock;
        private RazorLanguageServer? _server;
        private IDisposable? _serverShutdownDisposable;
        private LogHubLoggerProvider? _loggerProvider;

        private const string RazorLSPLogLevel = "RAZOR_TRACE";

        [ImportingConstructor]
        public RazorLanguageServerClient(
            RazorLanguageServerCustomMessageTarget customTarget,
            RazorLanguageClientMiddleLayer middleLayer,
            LSPRequestInvoker requestInvoker,
            ProjectConfigurationFilePathStore projectConfigurationFilePathStore,
            RazorLanguageServerLogHubLoggerProviderFactory logHubLoggerProviderFactory,
            VSLanguageServerFeatureOptions vsLanguageServerFeatureOptions,
            VSHostServicesProvider vsHostWorkspaceServicesProvider)
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

            if (vsLanguageServerFeatureOptions is null)
            {
                throw new ArgumentNullException(nameof(vsLanguageServerFeatureOptions));
            }

            if (vsHostWorkspaceServicesProvider is null)
            {
                throw new ArgumentNullException(nameof(vsHostWorkspaceServicesProvider));
            }

            _customMessageTarget = customTarget;
            _middleLayer = middleLayer;
            _requestInvoker = requestInvoker;
            _projectConfigurationFilePathStore = projectConfigurationFilePathStore;
            _logHubLoggerProviderFactory = logHubLoggerProviderFactory;
            _vsLanguageServerFeatureOptions = vsLanguageServerFeatureOptions;
            _vsHostWorkspaceServicesProvider = vsHostWorkspaceServicesProvider;
            _shutdownLock = new object();
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

        public event AsyncEventHandler<EventArgs>? StartAsync;
        public event AsyncEventHandler<EventArgs>? StopAsync
        {
            add { }
            remove { }
        }

        public async Task<Connection?> ActivateAsync(CancellationToken token)
        {
            // Swap to background thread, nothing below needs to be done on the UI thread.
            await TaskScheduler.Default;

            var (clientStream, serverStream) = FullDuplexStream.CreatePair();

            await EnsureCleanedUpServerAsync(token).ConfigureAwait(false);

            var traceLevel = GetVerbosity();

            // Initialize Logging Infrastructure
            _loggerProvider = (LogHubLoggerProvider)await _logHubLoggerProviderFactory.GetOrCreateAsync(LogFileIdentifier, token).ConfigureAwait(false);

            _server = await RazorLanguageServer.CreateAsync(serverStream, serverStream, traceLevel, ConfigureLanguageServer).ConfigureAwait(false);

            // Fire and forget for Initialized. Need to allow the LSP infrastructure to run in order to actually Initialize.
            _server.InitializedAsync(token).FileAndForget("RazorLanguageServerClient_ActivateAsync");

            var connection = new Connection(clientStream, clientStream);
            return connection;
        }

        private void ConfigureLanguageServer(RazorLanguageServerBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var services = builder.Services;
            services.AddLogging(logging =>
            {
                logging.AddFilter<LogHubLoggerProvider>(level => true);
                logging.AddProvider(_loggerProvider);
            });
            services.AddSingleton<LanguageServerFeatureOptions>(_vsLanguageServerFeatureOptions);
            services.AddSingleton<HostServicesProvider>(_vsHostWorkspaceServicesProvider);
        }

        private Trace GetVerbosity()
        {
            Trace result;

            // Since you can't set an Environment variable in CodeSpaces we need to default that scenario to Verbose.
            if (IsVSServer())
            {
                result = Trace.Verbose;
            }
            else
            {
                var logString = Environment.GetEnvironmentVariable(RazorLSPLogLevel);
                result = Enum.TryParse<Trace>(logString, out var parsedTrace) ? parsedTrace : Trace.Off;
            }

            return result;
        }

        /// <summary>
        /// Returns true if the client is a CodeSpace instance.
        /// </summary>
        protected virtual bool IsVSServer()
        {
            var shell = AsyncPackage.GetGlobalService(typeof(SVsShell)) as IVsShell;
            var result = shell!.GetProperty((int)__VSSPROPID11.VSSPROPID_ShellMode, out var mode);

            var isVSServer = ErrorHandler.Succeeded(result) && (int)mode == (int)__VSShellMode.VSSM_Server;
            return isVSServer;
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

            lock (_shutdownLock)
            {
                if (_server is not null)
                {
                    // Server still hasn't shutdown, attempt an ungraceful shutdown.
                    _server.Dispose();

                    ServerShutdown();
                }
            }
        }

        public async Task OnLoadedAsync()
        {
            await StartAsync.InvokeAsync(this, EventArgs.Empty).ConfigureAwait(false);
        }

        public Task OnServerInitializedAsync()
        {
            _serverShutdownDisposable = _server!.OnShutdown.Subscribe((_) => ServerShutdown());

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

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void ProjectConfigurationFilePathStore_Changed(object sender, ProjectConfigurationFilePathChangedEventArgs args)
#pragma warning restore VSTHRD100 // Avoid async void methods
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
                    CancellationToken.None);
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
    }
}
