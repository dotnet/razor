// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Shell;

internal static class IAsyncServiceProviderExtensions
{
    /// <summary>
    /// Returns a service without doing a transition to the UI thread to cast the service to the interface type.
    /// This should only be called for services that are well-understood to be castable off the UI thread, either
    /// because they are managed or free-threaded COM.
    /// </summary>
    public static async ValueTask<TInterface?> GetFreeThreadedServiceAsync<TService, TInterface>(this IAsyncServiceProvider serviceProvider)
        where TService : class
        where TInterface : class
    {
        return (TInterface?)await serviceProvider.GetServiceAsync(typeof(TService)).ConfigureAwait(false);
    }
}
