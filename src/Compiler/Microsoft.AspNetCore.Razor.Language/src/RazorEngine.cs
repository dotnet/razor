// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract class RazorEngine
{
    public abstract IReadOnlyList<IRazorEngineFeature> Features { get; }
    public abstract IReadOnlyList<IRazorEnginePhase> Phases { get; }

    public abstract void Process(RazorCodeDocument document);

#nullable restore
    internal TFeature? GetFeature<TFeature>()
    {
        var count = Features.Count;
        for (var i = 0; i < count; i++)
        {
            if (Features[i] is TFeature feature)
            {
                return feature;
            }
        }

        return default;
    }
#nullable disable
}

