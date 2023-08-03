// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Razor;

internal class RemoteTagHelperResolver : ITagHelperResolver
{
    private readonly static RazorConfiguration s_defaultConfiguration = FallbackRazorConfiguration.MVC_2_0;

    private readonly IFallbackProjectEngineFactory _fallbackFactory;
    private readonly CompilationTagHelperResolver _compilationTagHelperResolver;

    public RemoteTagHelperResolver(ITelemetryReporter telemetryReporter)
    {
        _fallbackFactory = new FallbackProjectEngineFactory();
        _compilationTagHelperResolver = new CompilationTagHelperResolver(telemetryReporter);
    }

    public ValueTask<TagHelperResolutionResult> GetTagHelpersAsync(
        Project workspaceProject,
        IProjectSnapshot projectSnapshot,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public ValueTask<TagHelperResolutionResult> GetTagHelpersAsync(
        Project workspaceProject,
        RazorConfiguration? configuration,
        string? factoryTypeName,
        CancellationToken cancellationToken)
    {
        if (workspaceProject is null)
        {
            throw new ArgumentNullException(nameof(workspaceProject));
        }

        if (configuration is null || workspaceProject is null)
        {
            return new(TagHelperResolutionResult.Empty);
        }

        var projectEngine = CreateProjectEngine(configuration, factoryTypeName);

        return _compilationTagHelperResolver.GetTagHelpersAsync(workspaceProject, projectEngine, cancellationToken);
    }

    internal RazorProjectEngine CreateProjectEngine(RazorConfiguration? configuration, string? factoryTypeName)
    {
        // This section is really similar to the code DefaultProjectEngineFactoryService
        // but with a few differences that are significant in the remote scenario
        //
        // Most notably, we are going to find the Tag Helpers using a compilation, and we have
        // no editor settings.
        //
        // The default configuration currently matches MVC-2.0. Beyond MVC-2.0 we added SDK support for
        // properly detecting project versions, so that's a good version to assume when we can't find a
        // configuration.
        configuration ??= s_defaultConfiguration;

        // If there's no factory to handle the configuration then fall back to a very basic configuration.
        //
        // This will stop a crash from happening in this case (misconfigured project), but will still make
        // it obvious to the user that something is wrong.
        var factory = CreateFactory(factoryTypeName) ?? _fallbackFactory;
        return factory.Create(configuration, RazorProjectFileSystem.Empty, b => { });
    }

    private static IProjectEngineFactory? CreateFactory(string? factoryTypeName)
    {
        if (factoryTypeName is null)
        {
            return null;
        }

        return (IProjectEngineFactory)Activator.CreateInstance(Type.GetType(factoryTypeName, throwOnError: true));
    }
}
