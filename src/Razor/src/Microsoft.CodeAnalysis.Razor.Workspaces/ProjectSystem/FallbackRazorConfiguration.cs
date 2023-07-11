﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class FallbackRazorConfiguration : RazorConfiguration
{
    public static readonly RazorConfiguration MVC_1_0 = new FallbackRazorConfiguration(
        RazorLanguageVersion.Version_1_0,
        "MVC-1.0",
        new[] { new FallbackRazorExtension("MVC-1.0"), });

    public static readonly RazorConfiguration MVC_1_1 = new FallbackRazorConfiguration(
        RazorLanguageVersion.Version_1_1,
        "MVC-1.1",
        new[] { new FallbackRazorExtension("MVC-1.1"), });

   public static readonly RazorConfiguration MVC_2_0 = new FallbackRazorConfiguration(
        RazorLanguageVersion.Version_2_0,
        "MVC-2.0",
        new[] { new FallbackRazorExtension("MVC-2.0"), });

    public static readonly RazorConfiguration MVC_2_1 = new FallbackRazorConfiguration(
         RazorLanguageVersion.Version_2_1,
         "MVC-2.1",
         new[] { new FallbackRazorExtension("MVC-2.1"), });

    public static readonly RazorConfiguration MVC_3_0 = new FallbackRazorConfiguration(
         RazorLanguageVersion.Version_3_0,
         "MVC-3.0",
         new[] { new FallbackRazorExtension("MVC-3.0"), });

    public static readonly RazorConfiguration MVC_5_0 = new FallbackRazorConfiguration(
         RazorLanguageVersion.Version_5_0,
         // Razor 5.0 uses MVC 3.0 Razor configuration.
         "MVC-3.0",
         new[] { new FallbackRazorExtension("MVC-3.0"), });

    public static readonly RazorConfiguration Latest = new FallbackRazorConfiguration(
         RazorLanguageVersion.Latest,
         // Razor latest uses MVC 3.0 Razor configuration.
         "MVC-3.0",
         new[] { new FallbackRazorExtension("MVC-3.0"), });

    public static RazorConfiguration SelectConfiguration(Version version)
    {
        if (version.Major == 1 && version.Minor == 0)
        {
            return MVC_1_0;
        }
        else if (version.Major == 1 && version.Minor == 1)
        {
            return MVC_1_1;
        }
        else if (version.Major == 2 && version.Minor == 0)
        {
            return MVC_2_0;
        }
        else if (version.Major == 2 && version.Minor >= 1)
        {
            return MVC_2_1;
        }
        else if (version.Major == 3 && version.Minor == 0)
        {
            return MVC_3_0;
        }
        else if (version.Major == 5 && version.Minor == 0)
        {
            return MVC_5_0;
        }
        else
        {
            return Latest;
        }
    }

    public FallbackRazorConfiguration(
        RazorLanguageVersion languageVersion,
        string configurationName,
        RazorExtension[] extensions,
        bool useConsolidatedMvcViews = false)
    {
        if (languageVersion is null)
        {
            throw new ArgumentNullException(nameof(languageVersion));
        }

        if (configurationName is null)
        {
            throw new ArgumentNullException(nameof(configurationName));
        }

        if (extensions is null)
        {
            throw new ArgumentNullException(nameof(extensions));
        }

        LanguageVersion = languageVersion;
        ConfigurationName = configurationName;
        Extensions = extensions;
        UseConsolidatedMvcViews = useConsolidatedMvcViews;
    }

    public override string ConfigurationName { get; }

    public override IReadOnlyList<RazorExtension> Extensions { get; }

    public override RazorLanguageVersion LanguageVersion { get; }

    public override bool UseConsolidatedMvcViews { get; }
}
