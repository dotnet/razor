// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

internal class DefaultRazorParserOptionsFactoryProjectFeature : RazorProjectEngineFeatureBase, IRazorParserOptionsFactoryProjectFeature
{
    private ImmutableArray<IConfigureRazorParserOptionsFeature> _configureOptions;

    protected override void OnInitialized()
    {
        _configureOptions = ProjectEngine.Engine.GetFeatures<IConfigureRazorParserOptionsFeature>().OrderByAsArray(static x => x.Order);
    }

    public RazorParserOptions Create(string fileKind, Action<RazorParserOptions.Builder> configure)
    {
        var configuration = ProjectEngine.Configuration;
        var builder = new RazorParserOptions.Builder(configuration.LanguageVersion, fileKind);

        configure?.Invoke(builder);

        foreach (var options in _configureOptions)
        {
            options.Configure(builder);
        }

        return builder.ToOptions();
    }
}
