// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorConfigurationEndpoint : IDidChangeConfigurationEndpoint
    {
        private readonly RazorLSPOptionsMonitor _optionsMonitor;
        private readonly ILogger _logger;

        public RazorConfigurationEndpoint(RazorLSPOptionsMonitor optionsMonitor, ILoggerFactory loggerFactory)
        {
            if (optionsMonitor is null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _optionsMonitor = optionsMonitor;
            _logger = loggerFactory.CreateLogger<RazorConfigurationEndpoint>();
        }

        public async Task<Unit> Handle(DidChangeConfigurationParamsBridge request, CancellationToken cancellationToken)
        {
            _logger.LogTrace("Settings changed. Updating the server.");

            await _optionsMonitor.UpdateAsync(cancellationToken);

            return new Unit();
        }
    }
}
