// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorLSPOptionsMonitor : IOptionsMonitor<RazorLSPOptions>
    {
        private readonly ILanguageServer _server;
        private readonly RazorConfigurationService _configurationService;
        private readonly IOptionsMonitorCache<RazorLSPOptions> _cache;
        internal event Action<RazorLSPOptions, string> _onChange;
        private RazorLSPOptions _currentValue;

        public RazorLSPOptionsMonitor(ILanguageServer server, RazorConfigurationService configurationService, IOptionsMonitorCache<RazorLSPOptions> cache)
        {
            if (server is null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            if (configurationService is null)
            {
                throw new ArgumentNullException(nameof(configurationService));
            }

            if (cache is null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            _server = server;
            _configurationService = configurationService;
            _cache = cache;
            Initialize();
        }

        public RazorLSPOptions CurrentValue => Get(Options.DefaultName);

        public RazorLSPOptions Get(string name)
        {
            name = name ?? Options.DefaultName;
            return _cache.GetOrAdd(name, () => _currentValue);
        }

        public IDisposable OnChange(Action<RazorLSPOptions, string> listener)
        {
            var disposable = new ChangeTrackerDisposable(this, listener);
            _onChange += disposable.OnChange;
            return disposable;
        }

        public async Task UpdateAsync()
        {
            var latestOptions = await _configurationService.GetLatestOptions();
            if (latestOptions != null)
            {
                _currentValue = latestOptions;
                InvokeChanged();
            }
        }

        private void InvokeChanged()
        {
            var name = Options.DefaultName;
            _cache.TryRemove(name);
            var options = Get(name);
            if (_onChange != null)
            {
                _onChange.Invoke(options, name);
            }
        }

        private void Initialize()
        {
            _currentValue = new RazorLSPOptions();
            _server.OnDidChangeConfiguration(async (request, token) =>
            {
                await UpdateAsync();
                return new Unit();
            });
        }

        internal class ChangeTrackerDisposable : IDisposable
        {
            private readonly Action<RazorLSPOptions, string> _listener;
            private readonly RazorLSPOptionsMonitor _monitor;

            public ChangeTrackerDisposable(RazorLSPOptionsMonitor monitor, Action<RazorLSPOptions, string> listener)
            {
                _listener = listener;
                _monitor = monitor;
            }

            public void OnChange(RazorLSPOptions options, string name) => _listener.Invoke(options, name);

            public void Dispose() => _monitor._onChange -= OnChange;
        }
    }
}
