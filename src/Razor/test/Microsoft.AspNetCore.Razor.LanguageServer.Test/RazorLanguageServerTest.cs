// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Nerdbank.Streams;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

public class RazorLanguageServerTest : TestBase
{
    public RazorLanguageServerTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        LspLogger = new LoggerAdapter(Logger);
    }

    private ILspLogger LspLogger { get; }

    [Fact]
    public async Task AllHandlersRegisteredAsync()
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();
        await using var server = RazorLanguageServerWrapper.Create(serverStream, serverStream, LspLogger);

        var innerServer = server.GetInnerLanguageServerForTesting();
        var handlerProvider = innerServer.GetTestAccessor().GetHandlerProvider();

        var registeredMethods = handlerProvider.GetRegisteredMethods();
        var handlerTypes = typeof(RazorLanguageServerWrapper).Assembly.GetTypes()
            .Where(t => typeof(IMethodHandler).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        if (registeredMethods.Length != handlerTypes.Count())
        {
            var unregisteredHandlers = handlerTypes.Where(t => !registeredMethods.Any(m => m.MethodName == GetMethodFromType(t)));
            Assert.True(false, $"Unregistered handlers: {string.Join(";", unregisteredHandlers.Select(t => t.Name))}");
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
}
