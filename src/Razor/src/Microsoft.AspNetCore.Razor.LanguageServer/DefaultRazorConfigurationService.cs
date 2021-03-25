// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultRazorConfigurationService : RazorConfigurationService
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

        public async override Task<RazorLSPOptions> GetLatestOptionsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var request = GenerateConfigParams();

                var response = await _server.SendRequestAsync("workspace/configuration", request);
                var result = await response.Returning<JObject[]>(cancellationToken);

                // LSP spec indicates result should be the same length as the number of ConfigurationItems we pass in.
                if (result == null || result.Length < 3 || result[0] == null)
                {
                    _logger.LogWarning("Client failed to provide the expected configuration.");
                    return null;
                }

                var instance = BuildOptions(result);
                return instance;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to sync client configuration on the server: {ex}");
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
                        },
                    }
            };
        }

        // Internal for testing
        internal static RazorLSPOptions BuildOptions(JObject[] result)
        {
            var defaultOptions = RazorLSPOptions.Default;

            UpdateVSCodeOptions(defaultOptions, result, out var trace, out var enableFormatting, out var autoClosingTags);
            UpdateVSOptions(defaultOptions, result, out var insertSpaces, out var tabSize);

            return new RazorLSPOptions(trace, enableFormatting, autoClosingTags, insertSpaces, tabSize);
        }

        private static void UpdateVSCodeOptions(
            RazorLSPOptions defaultOptions,
            JObject[] result,
            out Trace trace,
            out bool enableFormatting,
            out bool autoClosingTags)
        {
            var razor = result[0];
            var html = result[1];

            trace = defaultOptions.Trace;
            if (razor.TryGetValue("trace", out var parsedTrace))
            {
                trace = JTokenToObject(parsedTrace, trace);
            }

            enableFormatting = defaultOptions.EnableFormatting;
            if (razor.TryGetValue("format", out var parsedFormat))
            {
                if (parsedFormat is JObject jObject &&
                    jObject.TryGetValue("enable", out var parsedEnableFormatting))
                {
                    enableFormatting = JTokenToObject(parsedEnableFormatting, enableFormatting);
                }
            }

            autoClosingTags = defaultOptions.AutoClosingTags;
            if (html.TryGetValue("autoClosingTags", out var parsedAutoClosingTags))
            {
                autoClosingTags = JTokenToObject(parsedAutoClosingTags, autoClosingTags);
            }
        }

        private static void UpdateVSOptions(
            RazorLSPOptions defaultOptions,
            JObject[] result,
            out bool insertSpaces,
            out int tabSize)
        {
            var vsEditor = result[2];

            insertSpaces = defaultOptions.InsertSpaces;
            if (vsEditor.TryGetValue(nameof(EditorSettings.IndentWithTabs), out var parsedInsertTabs))
            {
                insertSpaces = !JTokenToObject(parsedInsertTabs, insertSpaces);
            }

            tabSize = defaultOptions.TabSize;
            if (vsEditor.TryGetValue(nameof(EditorSettings.IndentSize), out var parsedTabSize))
            {
                tabSize = JTokenToObject(parsedTabSize, tabSize);
            }
        }

        private static T JTokenToObject<T>(JToken token, T defaultValue)
        {
            try
            {
                // JToken.ToObject could potentially throw here if the user provides malformed options.
                // If this occurs, catch the exception and return the default value.
                return token.ToObject<T>();
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
