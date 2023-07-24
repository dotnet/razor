// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Utilities;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.Checksums;

public class TagHelperChecksumBenchmarks
{
    [ParamsAllValues]
    public ResourceSet ResourceSet { get; set; }

    private IReadOnlyList<TagHelperDescriptor> TagHelpers
        => ResourceSet switch
        {
            ResourceSet.Telerik => CommonResources.TelerikTagHelpers,
            _ => CommonResources.LegacyTagHelpers
        };

    [Benchmark(Description = "Create Checksums")]
    public void CreateChecksums()
    {
        foreach (var descriptor in TagHelpers)
        {
            _ = descriptor.CreateChecksum();
        }
    }
}
