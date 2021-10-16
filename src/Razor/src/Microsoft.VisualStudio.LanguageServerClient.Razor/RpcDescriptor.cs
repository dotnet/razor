// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.ServiceHub.Framework;

namespace Microsoft.VisualStudio.RazorExtension
{
    public static class RpcDescriptor
    {
        public static ServiceRpcDescriptor InteractiveServiceDescriptor { get; } = new ServiceJsonRpcDescriptor(
            new ServiceMoniker("RazorLanguageServer"),
            null,
            ServiceJsonRpcDescriptor.Formatters.UTF8,
            ServiceJsonRpcDescriptor.MessageDelimiters.HttpLikeHeaders);
    }
}
