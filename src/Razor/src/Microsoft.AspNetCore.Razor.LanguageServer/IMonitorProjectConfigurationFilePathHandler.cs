// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    [Parallel, Method(LanguageServerConstants.RazorMonitorProjectConfigurationFilePathEndpoint)]
    internal interface IMonitorProjectConfigurationFilePathHandler : IJsonRpcNotificationHandler<MonitorProjectConfigurationFilePathParams>
    {
    }
}
