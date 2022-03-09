// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    [JsonConverter(typeof(RazorConfigurationJsonConverter))]
    internal class ProjectSystemRazorConfiguration : RazorConfiguration
    {
        public ProjectSystemRazorConfiguration(
            RazorLanguageVersion languageVersion!!,
            string configurationName!!,
            RazorExtension[] extensions!!,
            bool useConsolidatedMvcViews = false)
        {
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
}
