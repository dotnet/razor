// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class TagHelperDiscoveryResult(
    TagHelperCollection collection,
    ImmutableArray<(string ProviderName, TimeSpan Elapsed)> timings)
{
    public static readonly TagHelperDiscoveryResult Empty = new(collection: [], timings: []);

    public TagHelperCollection Collection => collection;
    public ImmutableArray<(string ProviderName, TimeSpan Elapsed)> Timings => timings;

    public bool HasTimings => !timings.IsDefaultOrEmpty;
}
