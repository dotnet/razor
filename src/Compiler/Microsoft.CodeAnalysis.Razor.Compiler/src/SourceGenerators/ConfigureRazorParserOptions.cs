// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal class ConfigureRazorParserOptions(bool useRoslynTokenizer) : RazorEngineFeatureBase, IConfigureRazorParserOptionsFeature
{
    public int Order { get; set; }

    public void Configure(RazorParserOptionsBuilder options)
    {
        options.UseRoslynTokenizer = useRoslynTokenizer;
    }
}
