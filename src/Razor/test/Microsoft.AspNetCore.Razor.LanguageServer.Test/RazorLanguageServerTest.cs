﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Nerdbank.Streams;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

public class RazorLanguageServerTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [Fact]
    public async Task LocaleIsSetCorrectly()
    {
        var (_, serverStream) = FullDuplexStream.CreatePair();
        using var host = CreateLanguageServerHost(serverStream, serverStream);

        var server = host.GetTestAccessor().Server;
        server.Initialize();
        var queue = server.GetTestAccessor().GetRequestExecutionQueue();

        var initializeParams = new InitializeParams
        {
            Capabilities = new(),
            Locale = "de-DE"
        };

        await queue.ExecuteAsync<InitializeParams, InitializeResult>(initializeParams, Methods.InitializeName, LanguageServerConstants.DefaultLanguageName, server.GetLspServices(), DisposalToken);

        // We have to send one more request, because culture is set before any request starts, but the first initialize request has to
        // be started in order to set the culture.
        await queue.ExecuteAsync<VSInternalWorkspaceDiagnosticsParams, VSInternalWorkspaceDiagnosticReport[]>(new(), VSInternalMethods.WorkspacePullDiagnosticName, LanguageServerConstants.DefaultLanguageName, server.GetLspServices(), DisposalToken);

        var cultureInfo = queue.GetTestAccessor().GetCultureInfo();

        Assert.NotNull(cultureInfo);
        Assert.Equal("de-DE", cultureInfo.Name);
    }

    [Fact]
    public void AllHandlersRegisteredAsync()
    {
        var (_, serverStream) = FullDuplexStream.CreatePair();
        using var host = CreateLanguageServerHost(serverStream, serverStream);

        var server = host.GetTestAccessor().Server;
        var handlerProvider = server.GetTestAccessor().HandlerProvider;

        var registeredMethods = handlerProvider.GetRegisteredMethods();
        var handlerTypes = typeof(RazorLanguageServerHost).Assembly.GetTypes()
            .Where(t => typeof(IMethodHandler).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        // We turn this into a Set to handle cases like Completion where we have two handlers, only one of which will be registered
        // CLaSP will throw if two handlers register for the same method, so if THAT doesn't hold it's a CLaSP bug, not a Razor bug.
        var typeMethods = handlerTypes.Select(t => GetMethodFromType(t)).ToHashSet();

        if (registeredMethods.Length != typeMethods.Count)
        {
            var unregisteredHandlers = typeMethods.Where(t => !registeredMethods.Any(m => m.MethodName == t));
            Assert.Fail($"Unregistered handlers: {string.Join(";", unregisteredHandlers.Select(t => t))}");
        }

        static string GetMethodFromType(Type t)
        {
            var attribute = t.GetCustomAttribute<LanguageServerEndpointAttribute>();
            if (attribute is null)
            {
                foreach (var inter in t.GetInterfaces())
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

    private RazorLanguageServerHost CreateLanguageServerHost(Stream input, Stream output)
    {
        return RazorLanguageServerHost.Create(
            input,
            output,
            LoggerFactory,
            NoOpTelemetryReporter.Instance,
            configureServices: s =>
            {
                s.AddSingleton<IRazorProjectInfoDriver, TestProjectInfoDriver>();

                // VS Code only handler is added by rzls, but add here for testing purposes
                s.AddHandler<RazorNamedPipeConnectHandler>();
            });
    }

    private class TestProjectInfoDriver : IRazorProjectInfoDriver
    {
        public void AddListener(IRazorProjectInfoListener listener)
        {
        }

        public ImmutableArray<RazorProjectInfo> GetLatestProjectInfo()
        {
            return ImmutableArray<RazorProjectInfo>.Empty;
        }

        public Task WaitForInitializationAsync()
        {
            return Task.CompletedTask;
        }
    }
}
