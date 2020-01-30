// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorConfigurationEndpoint : IDidChangeConfigurationHandler
    {
        private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor;
        private readonly ILogger _logger;
        private DidChangeConfigurationCapability _capability;

        public RazorConfigurationEndpoint(IOptionsMonitor<RazorLSPOptions> optionsMonitor, ILoggerFactory loggerFactory)
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

        public object GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector
            };
        }

        public async Task<Unit> Handle(DidChangeConfigurationParams request, CancellationToken cancellationToken)
        {
            if (_optionsMonitor is RazorLSPOptionsMonitor razorLSPOptions)
            {
                _logger.LogTrace("Settings changed. Updating the server.");

                await razorLSPOptions.UpdateAsync();
            }

            return new Unit();
        }

        public void SetCapability(DidChangeConfigurationCapability capability)
        {
            _capability = capability;
        }
    }
}
