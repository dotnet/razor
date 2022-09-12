// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;
using Microsoft.AspNetCore.Razor.LanguageServer.Debugging;
using Microsoft.AspNetCore.Razor.LanguageServer.Definition;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentColor;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentHighlighting;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Folding;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Hover;
using Microsoft.AspNetCore.Razor.LanguageServer.Implementation;
using Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc;
using Microsoft.AspNetCore.Razor.LanguageServer.LinkedEditingRange;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Refactoring;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Microsoft.AspNetCore.Razor.LanguageServer.SignatureHelp;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.AspNetCore.Razor.LanguageServer.WrapWithTag;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Editor.Razor;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal sealed class RazorLanguageServer : IDisposable
    {
        private readonly ILanguageServer _innerServer;
        private readonly object _disposeLock;
        private bool _disposed;

        private RazorLanguageServer(ILanguageServer innerServer)
        {
            if (innerServer is null)
            {
                throw new ArgumentNullException(nameof(innerServer));
            }

            _innerServer = innerServer;
            _disposeLock = new object();
        }

        public IObservable<bool> OnShutdown => _innerServer.Shutdown;

        public Task WaitForExit => _innerServer.WaitForExit;

        public Task InitializedAsync(CancellationToken token) => _innerServer.Initialize(token);

        public static Task<RazorLanguageServer> CreateAsync(Stream input, Stream output, Trace trace, LanguageServerFeatureOptions? featureOptions = null, Action<RazorLanguageServerBuilder>? configure = null)
        {
            var serializer = new LspSerializer();
            serializer.RegisterRazorConverters();
            serializer.RegisterVSInternalExtensionConverters();

            ILanguageServer? server = null;
            var logLevel = RazorLSPOptions.GetLogLevelForTrace(trace);
            var initializedCompletionSource = new TaskCompletionSource<bool>();

            server = OmniSharp.Extensions.LanguageServer.Server.LanguageServer.PreInit(options =>
                options
                    .WithInput(input)
                    .WithOutput(output)
                    // StreamJsonRpc has both Serial and Parallel requests. With WithContentModifiedSupport(true) (which is default) when a Serial
                    // request is made any Parallel requests will be cancelled because the assumption is that Serial requests modify state, and that
                    // therefore any Parallel request is now invalid and should just try again. A specific instance of this can be seen when you
                    // hover over a TagHelper while the switch is set to true. Hover is parallel, and a lot of our endpoints like
                    // textDocument/_vs_onAutoInsert, and razor/languageQuery are Serial. I BELIEVE that specifically what happened is the serial
                    // languageQuery event gets fired by our semantic tokens endpoint (which fires constantly), cancelling the hover, which red-bars.
                    // We can prevent that behavior entirely by doing WithContentModifiedSupport, at the possible expense of some delays due doing all requests in serial.
                    //
                    // I recommend that we attempt to resolve this and switch back to WithContentModifiedSupport(true) in the future,
                    // I think that would mean either having 0 Serial Handlers in the whole LS, or making VSLanguageServerClient handle this more gracefully.
                    .WithContentModifiedSupport(false)
                    .WithSerializer(serializer)
                    .OnInitialized(async (s, request, response, cancellationToken) =>
                    {
                        var handlersManager = s.GetRequiredService<IHandlersManager>();
                        var jsonRpcHandlers = handlersManager.Descriptors.Select(d => d.Handler);
                        var registrationExtensions = jsonRpcHandlers.OfType<IRegistrationExtension>().Distinct();
                        var vsCapabilities = s.ClientSettings.Capabilities.ToVSClientCapabilities(serializer);
                        foreach (var registrationExtension in registrationExtensions)
                        {
                            var optionsResult = registrationExtension.GetRegistration(vsCapabilities);
                            if (optionsResult != null)
                            {
                                response.Capabilities.ExtensionData[optionsResult.ServerCapability] = JToken.FromObject(optionsResult.Options);
                            }
                        }

                        RazorLanguageServerCapability.AddTo(response.Capabilities);

                        var fileChangeDetectorManager = s.Services.GetRequiredService<RazorFileChangeDetectorManager>();
                        await fileChangeDetectorManager.InitializedAsync();

                        // Workaround for https://github.com/OmniSharp/csharp-language-server-protocol/issues/106
                        var languageServer = (OmniSharp.Extensions.LanguageServer.Server.LanguageServer)server!;
                        if (request.Capabilities?.Workspace?.Configuration.IsSupported == true)
                        {
                            // Initialize our options for the first time.
                            var optionsMonitor = languageServer.Services.GetRequiredService<RazorLSPOptionsMonitor>();

                            // Explicitly not passing in the same CancellationToken as that might get cancelled before the update happens.
                            _ = Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None)
                                .ContinueWith(async (_) => await optionsMonitor.UpdateAsync(), TaskScheduler.Default);
                        }
                    })
                    .WithHandler<RazorDocumentSynchronizationEndpoint>()

                    // These two are specifically commented out / manually added in the services below based on feature options (for now)
                    //.WithHandler<RazorCompletionEndpoint>()
                    //.WithHandler<RazorCompletionResolveEndpoint>()

                    .WithHandler<RazorHoverEndpoint>()
                    .WithHandler<RazorLanguageEndpoint>()
                    .WithHandler<RazorDiagnosticsEndpoint>()
                    .WithHandler<RazorConfigurationEndpoint>()
                    .WithHandler<RazorDocumentFormattingEndpoint>()
                    .WithHandler<RazorDocumentOnTypeFormattingEndpoint>()
                    .WithHandler<RazorDocumentRangeFormattingEndpoint>()
                    .WithHandler<RazorSemanticTokensEndpoint>()
                    .WithHandler<SemanticTokensRefreshEndpoint>()
                    .WithHandler<OnAutoInsertEndpoint>()
                    .WithHandler<CodeActionEndpoint>()
                    .WithHandler<CodeActionResolutionEndpoint>()
                    .WithHandler<MonitorProjectConfigurationFilePathEndpoint>()
                    .WithHandler<RenameEndpoint>()
                    .WithHandler<RazorDefinitionEndpoint>()
                    .WithHandler<LinkedEditingRangeEndpoint>()
                    .WithHandler<WrapWithTagEndpoint>()
                    .WithHandler<InlineCompletionEndpoint>()
                    .WithHandler<RazorBreakpointSpanEndpoint>()
                    .WithHandler<RazorProximityExpressionsEndpoint>()
                    .WithHandler<DocumentColorEndpoint>()
                    .WithHandler<FoldingRangeEndpoint>()
                    .WithHandler<TextDocumentTextPresentationEndpoint>()
                    .WithHandler<TextDocumentUriPresentationEndpoint>()
                    .WithHandler<DocumentHighlightEndpoint>()
                    .WithHandler<SignatureHelpEndpoint>()
                    .WithHandler<ImplementationEndpoint>()
                    .WithServices(services =>
                    {
                        featureOptions ??= new DefaultLanguageServerFeatureOptions();
                        services.AddSingleton(featureOptions);

                        if (featureOptions.SingleServerCompletionSupport)
                        {
                            options.WithHandler<RazorCompletionEndpoint>();
                            options.WithHandler<RazorCompletionResolveEndpoint>();
                        }
                        else
                        {
                            options.WithHandler<LegacyRazorCompletionEndpoint>();
                            options.WithHandler<LegacyRazorCompletionResolveEndpoint>();
                        }

                        services.AddLogging(builder => builder
                            .SetMinimumLevel(logLevel)
                            .AddLanguageProtocolLogging());

                        services.AddSingleton<ErrorReporter, LanguageServerErrorReporter>();

                        services.AddSingleton<RequestInvoker, RazorOmniSharpRequestInvoker>();

                        services.AddSingleton<DocumentContextFactory, DefaultDocumentContextFactory>();
                        services.AddSingleton<FilePathNormalizer>();
                        services.AddSingleton<ProjectSnapshotManagerDispatcher, LSPProjectSnapshotManagerDispatcher>();
                        services.AddSingleton<GeneratedDocumentPublisher, DefaultGeneratedDocumentPublisher>();
                        services.AddSingleton<AdhocWorkspaceFactory, DefaultAdhocWorkspaceFactory>();
                        services.AddSingleton<ProjectSnapshotChangeTrigger>((services) => services.GetRequiredService<GeneratedDocumentPublisher>());

                        services.AddSingleton<WorkspaceSemanticTokensRefreshPublisher, DefaultWorkspaceSemanticTokensRefreshPublisher>();
                        services.AddSingleton<ProjectSnapshotChangeTrigger, DefaultWorkspaceSemanticTokensRefreshTrigger>();

                        services.AddSingleton<DocumentVersionCache, DefaultDocumentVersionCache>();
                        services.AddSingleton<ProjectSnapshotChangeTrigger>((services) => services.GetRequiredService<DocumentVersionCache>());

                        services.AddSingleton<RemoteTextLoaderFactory, DefaultRemoteTextLoaderFactory>();
                        services.AddSingleton<ProjectResolver, DefaultProjectResolver>();
                        services.AddSingleton<DocumentResolver, DefaultDocumentResolver>();
                        services.AddSingleton<RazorProjectService, DefaultRazorProjectService>();
                        services.AddSingleton<ProjectSnapshotChangeTrigger, OpenDocumentGenerator>();
                        services.AddSingleton<RazorDocumentMappingService, DefaultRazorDocumentMappingService>();
                        services.AddSingleton<RazorFileChangeDetectorManager>();

                        services.AddSingleton<ProjectSnapshotChangeTrigger, RazorServerReadyPublisher>();

                        services.AddSingleton<ClientNotifierServiceBase, DefaultClientNotifierService>();

                        services.AddSingleton<IOnLanguageServerStarted, DefaultClientNotifierService>();

                        // Options
                        services.AddSingleton<RazorConfigurationService, DefaultRazorConfigurationService>();
                        services.AddSingleton<RazorLSPOptionsMonitor>();
                        services.AddSingleton<IOptionsMonitor<RazorLSPOptions>, RazorLSPOptionsMonitor>();

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

                        // Completion
                        services.AddSingleton<CompletionListCache>();
                        services.AddSingleton<AggregateCompletionListProvider>();
                        services.AddSingleton<DelegatedCompletionListProvider>();
                        services.AddSingleton<RazorCompletionListProvider>();
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

                        // Auto insert
                        services.AddSingleton<RazorOnAutoInsertProvider, CloseTextTagOnAutoInsertProvider>();
                        services.AddSingleton<RazorOnAutoInsertProvider, AutoClosingTagOnAutoInsertProvider>();

                        // Folding Range Providers
                        services.AddSingleton<RazorFoldingRangeProvider, RazorCodeBlockFoldingProvider>();

                        // Formatting
                        services.AddSingleton<RazorFormattingService, DefaultRazorFormattingService>();

                        // Formatting Passes
                        services.AddSingleton<IFormattingPass, HtmlFormattingPass>();
                        services.AddSingleton<IFormattingPass, CSharpFormattingPass>();
                        services.AddSingleton<IFormattingPass, CSharpOnTypeFormattingPass>();
                        services.AddSingleton<IFormattingPass, FormattingDiagnosticValidationPass>();
                        services.AddSingleton<IFormattingPass, FormattingContentValidationPass>();
                        services.AddSingleton<IFormattingPass, RazorFormattingPass>();

                        // Razor Code actions
                        services.AddSingleton<RazorCodeActionProvider, ExtractToCodeBehindCodeActionProvider>();
                        services.AddSingleton<RazorCodeActionResolver, ExtractToCodeBehindCodeActionResolver>();
                        services.AddSingleton<RazorCodeActionProvider, ComponentAccessibilityCodeActionProvider>();
                        services.AddSingleton<RazorCodeActionResolver, CreateComponentCodeActionResolver>();
                        services.AddSingleton<RazorCodeActionResolver, AddUsingsCodeActionResolver>();

                        // CSharp Code actions
                        services.AddSingleton<CSharpCodeActionProvider, TypeAccessibilityCodeActionProvider>();
                        services.AddSingleton<CSharpCodeActionProvider, DefaultCSharpCodeActionProvider>();
                        services.AddSingleton<CSharpCodeActionResolver, DefaultCSharpCodeActionResolver>();
                        services.AddSingleton<CSharpCodeActionResolver, AddUsingsCSharpCodeActionResolver>();
                        services.AddSingleton<CSharpCodeActionResolver, UnformattedRemappingCSharpCodeActionResolver>();

                        // Other
                        services.AddSingleton<RazorSemanticTokensInfoService, DefaultRazorSemanticTokensInfoService>();
                        services.AddSingleton<RazorHoverInfoService, DefaultRazorHoverInfoService>();
                        services.AddSingleton<HtmlFactsService, DefaultHtmlFactsService>();
                        services.AddSingleton<WorkspaceDirectoryPathResolver, DefaultWorkspaceDirectoryPathResolver>();
                        services.AddSingleton<RazorComponentSearchEngine, DefaultRazorComponentSearchEngine>();

                        if (configure != null)
                        {
                            var builder = new RazorLanguageServerBuilder(services);
                            configure(builder);
                        }

                        // Defaults: For when the caller hasn't provided them through the `configure` action.
                        services.TryAddSingleton<HostServicesProvider, DefaultHostServicesProvider>();
                    }));

            try
            {
                var factory = new LoggerFactory();
                var logger = factory.CreateLogger<RazorLanguageServer>();
                var assemblyInformationAttribute = typeof(RazorLanguageServer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                logger.LogInformation("Razor Language Server version {RazorVersion}", assemblyInformationAttribute.InformationalVersion);
                factory.Dispose();
            }
            catch
            {
                // Swallow exceptions from determining assembly information.
            }

            var razorLanguageServer = new RazorLanguageServer(server);

            IDisposable? exitSubscription = null;
            exitSubscription = server.Exit.Subscribe((_) =>
            {
                exitSubscription!.Dispose();
                razorLanguageServer.Dispose();
            });

            return Task.FromResult(razorLanguageServer);
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed)
                {
                    // Already disposed
                    return;
                }

                _disposed = true;

                TempDirectory.Instance.Dispose();
                _innerServer.Dispose();

                // Disposing the server doesn't actually dispose the servers Services for whatever reason. We cast the services collection
                // to IDisposable and try to dispose it ourselves to account for this.
                var disposableServices = _innerServer.Services as IDisposable;
                disposableServices?.Dispose();
            }
        }

        // For testing purposes only.
        internal ILanguageServer GetInnerLanguageServerForTesting()
        {
            return _innerServer;
        }
    }
}
