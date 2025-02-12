// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

#pragma warning disable CS0618 // Type or member is obsolete
internal sealed class DefaultRazorParserOptionsFeature : RazorEngineFeatureBase, IRazorParserOptionsFeature
#pragma warning restore CS0618 // Type or member is obsolete
{
    private ImmutableArray<IConfigureRazorParserOptionsFeature> _features;

    protected override void OnInitialized()
    {
        _features = Engine.GetFeatures<IConfigureRazorParserOptionsFeature>().OrderByAsArray(static x => x.Order);
    }

    public RazorParserOptions GetOptions()
    {
        var builder = new RazorParserOptionsBuilder(RazorLanguageVersion.Version_2_0, FileKinds.Legacy);

        foreach (var feature in _features)
        {
            feature.Configure(builder);
        }

        return builder.ToOptions();
    }
}
