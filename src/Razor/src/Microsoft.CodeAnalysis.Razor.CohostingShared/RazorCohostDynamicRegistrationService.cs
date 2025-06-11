﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

[Export(typeof(IRazorCohostStartupService))]
[method: ImportingConstructor]
internal class RazorCohostDynamicRegistrationService(
    LanguageServerFeatureOptions languageServerFeatureOptions,
    [ImportMany] IEnumerable<Lazy<IDynamicRegistrationProvider>> lazyRegistrationProviders,
    ILoggerFactory loggerFactory)
    : IRazorCohostStartupService
{
    private static readonly DocumentFilter[] s_filter = [new DocumentFilter()
    {
#if VSCODE
        Language = "aspnetcorerazor",
#else
        Language = CodeAnalysis.ExternalAccess.Razor.Cohost.Constants.RazorLanguageName,
#endif
        Pattern = "**/*.{razor,cshtml}"
    }];

    private readonly ImmutableArray<Lazy<IDynamicRegistrationProvider>> _lazyRegistrationProviders = [.. lazyRegistrationProviders];
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<RazorCohostDynamicRegistrationService>();

    public int Order => WellKnownStartupOrder.DynamicRegistration;

    public async Task StartupAsync(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext, CancellationToken cancellationToken)
    {
        if (!_languageServerFeatureOptions.UseRazorCohostServer)
        {
            return;
        }

        // We assume most registration providers will just return one, so whilst this isn't completely accurate, it's a
        // reasonable starting point
        using var registrations = new PooledArrayBuilder<Registration>(_lazyRegistrationProviders.Length);

        foreach (var provider in _lazyRegistrationProviders)
        {
            foreach (var registration in provider.Value.GetRegistrations(clientCapabilities, requestContext))
            {
                // We don't unregister anything, so we don't need to do anything interesting with Ids
                registration.Id = Guid.NewGuid().ToString();
                if (registration.RegisterOptions is ITextDocumentRegistrationOptions options)
                {
                    options.DocumentSelector = s_filter;
                }

                registrations.Add(registration);
            }
        }

        var razorCohostClientLanguageServerManager = requestContext.GetRequiredService<IRazorClientLanguageServerManager>();

        var allRegistrations = registrations.ToArray();
        _logger.LogInformation($"Requesting {allRegistrations.Length} Razor cohost registrations.");

        await razorCohostClientLanguageServerManager.SendRequestAsync(
            Methods.ClientRegisterCapabilityName,
            new RegistrationParams()
            {
                Registrations = allRegistrations
            },
            cancellationToken).ConfigureAwait(false);
    }
}
