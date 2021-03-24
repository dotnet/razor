// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
                var request = new ConfigurationParams()
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
                            Section = "editor"
                        },
                    }
                };

                var response = await _server.SendRequestAsync("workspace/configuration", request);
                var result = await response.Returning<JObject[]>(cancellationToken);

                // Spec indicates result should be the same length as the number of ConfigurationItems we pass in.
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

        private static RazorLSPOptions BuildOptions(JObject[] result)
        {
            var instance = RazorLSPOptions.Default;

            var razor = result[0];
            var html = result[1];
            var editor = result[2];

            var trace = instance.Trace;
            if (razor.TryGetValue("trace", out var parsedTrace))
            {
                trace = parsedTrace.ToObject<Trace>();
            }

            var enableFormatting = instance.EnableFormatting;
            if (razor.TryGetValue("format", out var parsedFormat))
            {
                if (((JObject)parsedFormat).TryGetValue("enable", out var parsedEnableFormatting))
                {
                    enableFormatting = parsedEnableFormatting.ToObject<bool>();
                }
            }

            var autoClosingTags = instance.AutoClosingTags;
            if (html.TryGetValue("autoClosingTags", out var parsedAutoClosingTags))
            {
                autoClosingTags = parsedAutoClosingTags.ToObject<bool>();
            }

            var insertSpaces = instance.InsertSpaces;
            if (editor.TryGetValue("InsertSpaces", out var parsedInsertSpaces))
            {
                insertSpaces = parsedInsertSpaces.ToObject<bool>();
            }

            var tabSize = instance.TabSize;
            if (editor.TryGetValue("TabSize", out var parsedTabSize))
            {
                tabSize = parsedTabSize.ToObject<int>();
            }

            return new RazorLSPOptions(trace, enableFormatting, autoClosingTags, insertSpaces, tabSize);
        }
    }
}
