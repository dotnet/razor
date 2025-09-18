// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Build;

namespace Microsoft.VisualStudio.Razor.ProjectSystem;

[ExportBuildGlobalPropertiesProvider(designTimeBuildProperties: true)]
[AppliesTo("DotNetCoreRazor")]
internal class CohostingDesignTimeBuildPropertyProvider : StaticGlobalPropertiesProviderBase
{
    private readonly Task<IImmutableDictionary<string, string>> _properties;

    /// <summary>
    /// Initializes a new instance of the <see cref="CohostingDesignTimeBuildPropertyProvider"/> class.
    /// </summary>
    [ImportingConstructor]
    internal CohostingDesignTimeBuildPropertyProvider(IProjectService projectService, LanguageServerFeatureOptions featureOptions)
        : base(projectService.Services)
    {
        var properties = Empty.PropertiesMap.Add(nameof(LanguageServerFeatureOptions.UseRazorCohostServer), featureOptions.UseRazorCohostServer ? "true" : "false");
        if (featureOptions.UseRazorCohostServer)
        {
            // When cohosting is enabled we always want to use the source generator. Since this property provider only runs in design-time builds,
            // this doesn't affect real build outputs.
            properties = properties.Add("UseRazorSourceGenerator", "true");
        }

        _properties = Task.FromResult<IImmutableDictionary<string, string>>(properties);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "No awaiting involved")]
    public override Task<IImmutableDictionary<string, string>> GetGlobalPropertiesAsync(CancellationToken cancellationToken)
    {
        return _properties;
    }
}
