// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class TestRazorLSPOptionsMonitor : RazorLSPOptionsMonitor
{
    private TestRazorLSPOptionsMonitor(
        IConfigurationSyncService configurationService,
        IOptionsMonitorCache<RazorLSPOptions> cache)
        : base(configurationService, cache, RazorLSPOptions.Default)
    {
    }

    public bool Called { get; private set; }

    public override Task UpdateAsync(CancellationToken cancellationToken)
    {
        Called = true;
        return base.UpdateAsync();
    }

    public static TestRazorLSPOptionsMonitor Create(
        IConfigurationSyncService? configurationService = null,
        IOptionsMonitorCache<RazorLSPOptions>? cache = null)
    {
        configurationService ??= Mock.Of<IConfigurationSyncService>(
           f => f.GetLatestOptionsAsync(CancellationToken.None) == Task.FromResult(RazorLSPOptions.Default),
           MockBehavior.Strict);

        cache ??= new ServiceCollection().AddOptions().BuildServiceProvider().GetRequiredService<IOptionsMonitorCache<RazorLSPOptions>>();

        return new(configurationService, cache);
    }
}
