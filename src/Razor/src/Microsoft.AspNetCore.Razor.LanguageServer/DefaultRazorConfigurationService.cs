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

                var builder = new ConfigurationBuilder();

                var configObject = new JObject
                {
                    { "razor", result[0] },
                    { "html", result[1] },
                    { "editor", result[2] }
                };
                var configJsonString = configObject.ToString();
                using var configStream = new MemoryStream(Encoding.UTF8.GetBytes(configJsonString));
                builder.AddJsonStream(configStream);

                var config = builder.Build();

                var instance = BuildOptions(config);
                return instance;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to sync client configuration on the server: {ex}");
                return null;
            }
        }

        private static RazorLSPOptions BuildOptions(IConfiguration config)
        {
            var instance = RazorLSPOptions.Default;

            Enum.TryParse(config["razor:trace"], out Trace trace);

            var enableFormatting = instance.EnableFormatting;
            if (bool.TryParse(config["razor:format:enable"], out var parsedEnableFormatting))
            {
                enableFormatting = parsedEnableFormatting;
            }

            var autoClosingTags = instance.AutoClosingTags;
            if (bool.TryParse(config["html:autoClosingTags"], out var parsedAutoClosingTags))
            {
                autoClosingTags = parsedAutoClosingTags;
            }

            var insertSpaces = instance.InsertSpaces;
            if (bool.TryParse(config["editor:insertSpaces"], out var parsedInsertSpaces))
            {
                insertSpaces = parsedInsertSpaces;
            }

            var tabSize = instance.TabSize;
            if (int.TryParse(config["editor:tabSize"], out var parsedTabSize))
            {
                tabSize = parsedTabSize;
            }

            return new RazorLSPOptions(trace, enableFormatting, autoClosingTags, insertSpaces, tabSize);
        }
    }
}
