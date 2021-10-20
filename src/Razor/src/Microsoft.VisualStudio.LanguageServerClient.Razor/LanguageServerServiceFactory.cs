// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using VSShell = Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.RazorExtension.ServiceHub
{
    public static class LanguageServerServiceFactory
    {
        public static async Task<IDuplexPipe?> CreateServiceAsync(CancellationToken cancellationToken)
        {
            if (VSShell.Package.GetGlobalService(typeof(VSShell.Interop.SAsyncServiceProvider)) is not VSShell.IAsyncServiceProvider serviceProvider)
            {
                return null;
            }

            var serviceContainer = await VSShell.ServiceExtensions.GetServiceAsync<
                VSShell.ServiceBroker.SVsBrokeredServiceContainer,
                VSShell.ServiceBroker.IBrokeredServiceContainer>(serviceProvider).ConfigureAwait(false);
            if (serviceContainer is null)
            {
                return null;
            }
            var sb = serviceContainer.GetFullAccessServiceBroker();

            return await sb.GetPipeAsync(RpcDescriptor.InteractiveServiceDescriptor.Moniker, cancellationToken);
        }
    }
}
