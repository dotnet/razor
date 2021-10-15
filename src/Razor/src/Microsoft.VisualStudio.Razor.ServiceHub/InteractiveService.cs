// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.VisualStudio.Razor.ServiceHub.Contracts;
using Nerdbank.Streams;

namespace Microsoft.VisualStudio.Razor.ServiceHub
{
    internal class InteractiveService : IInteractiveService
    {
        public Task<bool> IsRunning(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> Start(CancellationToken ct)
        {
            var trace = Trace.Messages;

            var (clientStream, serverStream) = FullDuplexStream.CreatePair();

            var server = await RazorLanguageServer.CreateAsync(serverStream, serverStream, trace);
            await server.InitializedAsync(ct);

            return true;
        }
    }
}
