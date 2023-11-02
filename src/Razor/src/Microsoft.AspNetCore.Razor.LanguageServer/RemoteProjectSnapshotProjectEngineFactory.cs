// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal sealed class RemoteProjectSnapshotProjectEngineFactory(IOptionsMonitor<RazorLSPOptions> optionsMonitor)
    : ProjectSnapshotProjectEngineFactory(s_fallbackProjectEngineFactory, MefProjectEngineFactories.Factories)
{
    private static readonly IFallbackProjectEngineFactory s_fallbackProjectEngineFactory = new FallbackProjectEngineFactory();

    private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor = optionsMonitor;

    public override RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure)
    {
        if (fileSystem is not DefaultRazorProjectFileSystem defaultFileSystem)
        {
            throw new ArgumentException("Unexpected file system.", nameof(fileSystem));
        }

        var remoteFileSystem = new RemoteRazorProjectFileSystem(defaultFileSystem.Root);
        return base.Create(configuration, remoteFileSystem, Configure);

        void Configure(RazorProjectEngineBuilder builder)
        {
            configure?.Invoke(builder);
            builder.Features.Add(new RemoteCodeGenerationOptionsFeature(_optionsMonitor));
        }
    }

    private class RemoteCodeGenerationOptionsFeature(IOptionsMonitor<RazorLSPOptions> optionsMonitor)
        : RazorEngineFeatureBase, IConfigureRazorCodeGenerationOptionsFeature
    {
        private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor = optionsMonitor;

        public int Order { get; set; }

        public void Configure(RazorCodeGenerationOptionsBuilder options)
        {
            // We don't need to explicitly subscribe to options changing because this method will be run on every parse.
            var currentOptions = _optionsMonitor.CurrentValue;

            options.IndentSize = currentOptions.TabSize;
            options.IndentWithTabs = !currentOptions.InsertSpaces;
        }
    }
}
