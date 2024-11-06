﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor;
using Microsoft.VisualStudio.Razor.LanguageClient.Cohost;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Test.Cohost;

public class CohostEndpointTest(ITestOutputHelper testOutputHelper) : ToolingTestBase(testOutputHelper)
{
    [Fact]
    public void EndpointsHaveUniqueLSPMethods()
    {
        var methods = new Dictionary<string, Type>();

        var endpoints = typeof(CohostColorPresentationEndpoint).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<CohostEndpointAttribute>() != null)
            .Select(t => (t, t.GetCustomAttribute<CohostEndpointAttribute>().Method));

        foreach (var (endpointType, method) in endpoints)
        {
            if (methods.TryGetValue(method, out var existing))
            {
                Assert.Fail($"Could not register {endpointType.Name} for {method} because {existing.Name} already has.");
            }

            methods.Add(method, endpointType);
        }
    }

    [Fact]
    public void RegistrationsProvideFilter()
    {
        var testComposition = TestComposition.Roslyn
            .Add(TestComposition.Editor)
            .AddAssemblies(typeof(CohostLinkedEditingRangeEndpoint).Assembly)
            .AddAssemblies(typeof(DefaultLSPRequestInvoker).Assembly)
            .AddAssemblies(typeof(LanguageServerFeatureOptions).Assembly)
            .AddParts(typeof(TestILanguageServiceBroker2))
            .AddExcludedPartTypes(typeof(IWorkspaceProvider))
            .AddParts(typeof(TestWorkspaceProvider))
            .AddExcludedPartTypes(typeof(ILspEditorFeatureDetector))
            .AddParts(typeof(TestLspEditorFeatureDetector));

        using var exportProvider = testComposition.ExportProviderFactory.CreateExportProvider();

        var providers = exportProvider.GetExportedValues<IDynamicRegistrationProvider>().ToList();

        // First we verify that the MEF composition above is correct, otherwise this test will be invalid
        var actualProviders = typeof(CohostLinkedEditingRangeEndpoint).Assembly.GetTypes().Where(t => !t.IsInterface && typeof(IDynamicRegistrationProvider).IsAssignableFrom(t)).ToList();
        Assert.Equal(actualProviders.OrderBy(a => a.Name).Select(r => r.Name).ToArray(), providers.OrderBy(e => e.GetType().Name).Select(r => r.GetType().Name).ToArray());

        var clientCapabilities = new VSInternalClientCapabilities()
        {
            SupportsVisualStudioExtensions = true,
            TextDocument = new TextDocumentClientCapabilities()
            {
                CodeAction = new() { DynamicRegistration = true },
                CodeLens = new() { DynamicRegistration = true },
                Completion = new() { DynamicRegistration = true },
                Definition = new() { DynamicRegistration = true },
                Diagnostic = new() { DynamicRegistration = true },
                DocumentHighlight = new() { DynamicRegistration = true },
                DocumentLink = new() { DynamicRegistration = true },
                DocumentSymbol = new() { DynamicRegistration = true },
                FoldingRange = new() { DynamicRegistration = true },
                Formatting = new() { DynamicRegistration = true },
                Hover = new() { DynamicRegistration = true },
                Implementation = new() { DynamicRegistration = true },
                InlayHint = new() { DynamicRegistration = true },
                LinkedEditingRange = new() { DynamicRegistration = true },
                OnTypeFormatting = new() { DynamicRegistration = true },
                RangeFormatting = new() { DynamicRegistration = true },
                References = new() { DynamicRegistration = true },
                Rename = new() { DynamicRegistration = true },
                SemanticTokens = new() { DynamicRegistration = true },
                SignatureHelp = new() { DynamicRegistration = true },
                Synchronization = new() { DynamicRegistration = true },
                TypeDefinition = new() { DynamicRegistration = true }
            },
        };

        foreach (var endpoint in providers)
        {
            if (endpoint is CohostSemanticTokensRangeEndpoint)
            {
                // We can't currently test this, as the GetRegistrations method calls requestContext.GetRequiredService
                // and we can't create a request context ourselves
                continue;
            }

            var registrations = endpoint.GetRegistrations(clientCapabilities, requestContext: new());

            // If we didn't get any registrations then the test is probably invalid, and we need to update client capabilities above
            if (registrations.Length == 0)
            {
                Assert.Fail($"Did not get any registrations from {endpoint.GetType().Name}. Client capabilities might be wrong?");
            }

            foreach (var registration in registrations)
            {
                var options = registration.RegisterOptions as ITextDocumentRegistrationOptions;
                if (options is null)
                {
                    Assert.Fail($"Could not convert registration options from {endpoint.GetType().Name} to {nameof(ITextDocumentRegistrationOptions)}. It was {registration.RegisterOptions?.GetType().Name}");
                }
            }
        }
    }

    [Export(typeof(ILanguageServiceBroker2))]
    private class TestILanguageServiceBroker2 : ILanguageServiceBroker2
    {
        public IEnumerable<ILanguageClientInstance> ActiveLanguageClients => throw new NotImplementedException();
        public IEnumerable<Lazy<ILanguageClient, IContentTypeMetadata>> FactoryLanguageClients => throw new NotImplementedException();
        public IEnumerable<Lazy<ILanguageClient, IContentTypeMetadata>> LanguageClients => throw new NotImplementedException();

        public event EventHandler<LanguageClientLoadedEventArgs> LanguageClientLoaded { add { } remove { } }
        public event AsyncEventHandler<LanguageClientNotifyEventArgs> ClientNotifyAsync { add { } remove { } }

        public void AddCustomBufferContentTypes(IEnumerable<string> contentTypes) => throw new NotImplementedException();
        public void AddLanguageClients(IEnumerable<Lazy<ILanguageClient, IContentTypeMetadata>> items) => throw new NotImplementedException();
        public Task LoadAsync(IContentTypeMetadata contentType, ILanguageClient client) => throw new NotImplementedException();
        public void Notify<T>(Notification<T> notification, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task NotifyAsync(ILanguageClient languageClient, string method, JToken parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task OnDidChangeTextDocumentAsync(ITextSnapshot before, ITextSnapshot after, IEnumerable<ITextChange> textChanges) => throw new NotImplementedException();
        public Task OnDidCloseTextDocumentAsync(ITextSnapshot snapShot) => throw new NotImplementedException();
        public Task OnDidOpenTextDocumentAsync(ITextSnapshot snapShot) => throw new NotImplementedException();
        public Task OnDidSaveTextDocumentAsync(ITextDocument document) => throw new NotImplementedException();
        public void RemoveCustomBufferContentTypes(IEnumerable<string> contentTypes) => throw new NotImplementedException();
        public void RemoveLanguageClients(IEnumerable<Lazy<ILanguageClient, IContentTypeMetadata>> items) => throw new NotImplementedException();
        public IAsyncEnumerable<(string client, TResponse? response)> RequestAllAsync<TRequest, TResponse>(Request<TRequest, TResponse> request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<(ILanguageClient?, JToken?)> RequestAsync(string[] contentTypes, Func<JToken, bool> capabilitiesFilter, string method, JToken parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<(ILanguageClient?, JToken?)> RequestAsync(string[] contentTypes, Func<JToken, bool> capabilitiesFilter, string clientName, string method, JToken parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ManualInvocationResponse?> RequestAsync(ITextBuffer textBuffer, Func<JToken, bool> capabilitiesFilter, string languageServerName, string method, Func<ITextSnapshot, JToken> parameterFactory, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<JToken?> RequestAsync(ILanguageClient languageClient, string method, JToken parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<TResponse?> RequestAsync<TRequest, TResponse>(Request<TRequest, TResponse> request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<(ILanguageClient?, TOut)> RequestAsync<TIn, TOut>(string[] contentTypes, Func<ServerCapabilities, bool> capabilitiesFilter, LspRequest<TIn, TOut> method, TIn parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<TOut> RequestAsync<TIn, TOut>(ILanguageClient languageClient, LspRequest<TIn, TOut> method, TIn parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IEnumerable<(ILanguageClient, JToken?)>> RequestMultipleAsync(string[] contentTypes, Func<JToken, bool> capabilitiesFilter, string method, JToken parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
        public IAsyncEnumerable<ManualInvocationResponse> RequestMultipleAsync(ITextBuffer textBuffer, Func<JToken, bool> capabilitiesFilter, string method, Func<ITextSnapshot, JToken> parameterFactory, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IEnumerable<(ILanguageClient, TOut)>> RequestMultipleAsync<TIn, TOut>(string[] contentTypes, Func<ServerCapabilities, bool> capabilitiesFilter, LspRequest<TIn, TOut> method, TIn parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    [Export(typeof(IWorkspaceProvider))]
    private class TestWorkspaceProvider : IWorkspaceProvider
    {
        public CodeAnalysis.Workspace GetWorkspace() => throw new NotImplementedException();
    }

    [Export(typeof(ILspEditorFeatureDetector))]
    private class TestLspEditorFeatureDetector : ILspEditorFeatureDetector
    {
        public bool IsLiveShareHost() => throw new NotImplementedException();
        public bool IsLspEditorEnabled() => throw new NotImplementedException();
        public bool IsLspEditorSupported(string documentFilePath) => throw new NotImplementedException();
        public bool IsRemoteClient() => throw new NotImplementedException();
    }

    [Export(typeof(IRazorSemanticTokensRefreshQueue))]
    private class TestRazorSemanticTokensRefreshQueue : IRazorSemanticTokensRefreshQueue
    {
        public void Initialize(string clientCapabilitiesString) => throw new NotImplementedException();
        public Task TryEnqueueRefreshComputationAsync(Project project, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
