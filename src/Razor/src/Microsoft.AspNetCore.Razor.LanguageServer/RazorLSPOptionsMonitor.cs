﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class RazorLSPOptionsMonitor : IOptionsMonitor<RazorLSPOptions>
{
    private readonly IConfigurationSyncService _configurationService;
    private readonly IOptionsMonitorCache<RazorLSPOptions> _cache;
    private event Action<RazorLSPOptions, string>? OnChangeEvent;
    private RazorLSPOptions _currentValue;

    public RazorLSPOptionsMonitor(IConfigurationSyncService configurationService, IOptionsMonitorCache<RazorLSPOptions> cache, RazorLSPOptions currentOptions)
    {
        if (configurationService is null)
        {
            throw new ArgumentNullException(nameof(configurationService));
        }

        if (cache is null)
        {
            throw new ArgumentNullException(nameof(cache));
        }

        _configurationService = configurationService;
        _cache = cache;
        _currentValue = currentOptions;
    }

    public RazorLSPOptions CurrentValue => Get(Options.DefaultName);

    public RazorLSPOptions Get(string name)
    {
        name ??= Options.DefaultName;
        return _cache.GetOrAdd(name, () => _currentValue);
    }

    public IDisposable OnChange(Action<RazorLSPOptions, string> listener)
    {
        var disposable = new ChangeTrackerDisposable(this, listener);
        OnChangeEvent += disposable.OnChange;
        return disposable;
    }

    public virtual async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        var latestOptions = await _configurationService.GetLatestOptionsAsync(cancellationToken).ConfigureAwait(false);
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
        OnChangeEvent?.Invoke(options, name);
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

        public void Dispose() => _monitor.OnChangeEvent -= OnChange;
    }
}
