// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using Microsoft.ServiceHub.Framework;

namespace Microsoft.VisualStudio.Razor.ServiceHub.Contracts
{
    public static class ServiceHubDescriptors
    {
        public static ServiceRpcDescriptor VSOptionsService = new ServiceJsonRpcDescriptor(
            new ServiceMoniker("Microsoft.VisualStudio.Options"),
            clientInterface: null,
            ServiceJsonRpcDescriptor.Formatters.UTF8,
            ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);
    }
}
