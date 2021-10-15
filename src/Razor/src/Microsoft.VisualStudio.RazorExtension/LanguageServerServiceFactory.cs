// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Razor.ServiceHub.Contracts;
using VSShell = Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.RazorExtension.ServiceHub
{
    public static class LanguageServerServiceFactory
    {
        public static async Task<IInteractiveService?> CreateServiceAsync(CancellationToken cancellationToken)
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

#pragma warning disable ISB001 // Dispose of proxies
            return await sb.GetProxyAsync<IInteractiveService>(RpcDescriptor.InteractiveServiceDescriptor, cancellationToken);
#pragma warning restore ISB001 // Dispose of proxies
        }
    }
}
