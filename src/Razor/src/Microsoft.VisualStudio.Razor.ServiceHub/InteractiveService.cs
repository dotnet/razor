// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.VisualStudio.Razor.ServiceHub.Contracts;

namespace Microsoft.VisualStudio.Razor.ServiceHub
{
    internal class InteractiveService : IInteractiveService
    {
        public Task<bool> IsRunning(CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<bool> Shutdown()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> Start(Stream input, Stream output, CancellationToken ct)
        {
            var trace = Trace.Messages;

            var server = await RazorLanguageServer.CreateAsync(input, output, trace);
            await server.InitializedAsync(ct);

            return true;
        }
    }
}
