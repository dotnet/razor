﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class DefaultRazorConfigurationService : IConfigurationSyncService
{
    private readonly ClientNotifierServiceBase _server;
    private readonly ILogger _logger;

    public DefaultRazorConfigurationService(ClientNotifierServiceBase languageServer, ILoggerFactory loggerFactory)
    {
        if (languageServer is null)
        {
            throw new ArgumentNullException(nameof(languageServer));
        }

        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _server = languageServer;
        _logger = loggerFactory.CreateLogger<DefaultRazorConfigurationService>();
    }

    public async Task<RazorLSPOptions?> GetLatestOptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = GenerateConfigParams();

            var result = await _server.SendRequestAsync<ConfigurationParams, JObject[]>(Methods.WorkspaceConfigurationName, request, cancellationToken).ConfigureAwait(false);

            // LSP spec indicates result should be the same length as the number of ConfigurationItems we pass in.
            if (result?.Length != request.Items.Length || result[0] is null)
            {
                _logger.LogWarning("Client failed to provide the expected configuration.");
                return null;
            }

            var instance = BuildOptions(result);
            return instance;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to sync client configuration on the server: {ex}", ex);
            return null;
        }
    }

    private static ConfigurationParams GenerateConfigParams()
    {
        // NOTE: Do not change the ordering of sections without updating
        // the code in the BuildOptions method below.
        return new ConfigurationParams()
        {
            Items = new[]
            {
                new ConfigurationItem()
                {
                    Section = "razor"
                },
                new ConfigurationItem()
                {
                    Section = "html"
                },
                new ConfigurationItem()
                {
                    Section = "vs.editor.razor"
                }
            }
        };
    }

    // Internal for testing
    internal RazorLSPOptions BuildOptions(JObject[] result)
    {
        // VS Code will send back settings in the first two elements, VS will send back settings in the 3rd
        // so we can effectively detect which IDE we're in.
        if (result[0] is null or { Count: 0 } && result[1] is null or { Count: 0 })
        {
            var settings = ExtractVSOptions(result);
            return RazorLSPOptions.From(settings);
        }
        else
        {
            ExtractVSCodeOptions(result, out var trace, out var enableFormatting, out var autoClosingTags);
            return new RazorLSPOptions(trace, enableFormatting, autoClosingTags, ClientSettings.Default);
        }
    }

    private void ExtractVSCodeOptions(
        JObject[] result,
        out Trace trace,
        out bool enableFormatting,
        out bool autoClosingTags)
    {
        var razor = result[0];
        var html = result[1];

        trace = RazorLSPOptions.Default.Trace;
        enableFormatting = RazorLSPOptions.Default.EnableFormatting;
        autoClosingTags = RazorLSPOptions.Default.AutoClosingTags;

        if (razor != null)
        {
            if (razor.TryGetValue("trace", out var parsedTrace))
            {
                trace = GetObjectOrDefault(parsedTrace, trace);
            }

            if (razor.TryGetValue("format", out var parsedFormat))
            {
                if (parsedFormat is JObject jObject &&
                    jObject.TryGetValue("enable", out var parsedEnableFormatting))
                {
                    enableFormatting = GetObjectOrDefault(parsedEnableFormatting, enableFormatting);
                }
            }
        }

        if (html != null)
        {
            if (html.TryGetValue("autoClosingTags", out var parsedAutoClosingTags))
            {
                autoClosingTags = GetObjectOrDefault(parsedAutoClosingTags, autoClosingTags);
            }
        }
    }

    private ClientSettings ExtractVSOptions(JObject[] result)
    {
        try
        {
            var settings = result[2]?.ToObject<ClientSettings>();
            if (settings is null)
            {
                return ClientSettings.Default;
            }

            // Deserializing can result in null properties. Fill with default as needed
            if (settings.ClientSpaceSettings is null)
            {
                settings = settings with { ClientSpaceSettings = ClientSpaceSettings.Default };
            }

            if (settings.AdvancedSettings is null)
            {
                settings = settings with { AdvancedSettings = ClientAdvancedSettings.Default };
            }

            return settings;
        }
        catch (JsonReaderException)
        {
            return ClientSettings.Default;
        }
    }

    private T GetObjectOrDefault<T>(JToken token, T defaultValue)
    {
        try
        {
            // JToken.ToObject could potentially throw here if the user provides malformed options.
            // If this occurs, catch the exception and return the default value.
            return token.ToObject<T>() ?? defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Malformed option: Token {token} cannot be converted to type {TypeOfT}.", token, typeof(T));
            return defaultValue;
        }
    }
}
