// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorConfigurationService
    {
        private readonly ILanguageServer _server;
        private readonly ILogger _logger;

        public RazorConfigurationService(ILanguageServer languageServer, ILoggerFactory loggerFactory)
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
            _logger = loggerFactory.CreateLogger<RazorConfigurationService>();
        }

        public async Task<RazorLSPOptions> GetLatestOptions()
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
                    }
                };

                var result = await _server.Client.SendRequest<ConfigurationParams, object[]>("workspace/configuration", request);
                if (result == null || result.Length < 1)
                {
                    return null;
                }

                var jsonString = result[0].ToString();
                var builder = new ConfigurationBuilder();
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
                builder.AddJsonStream(stream);
                var config = builder.Build();

                var instance = new RazorLSPOptions();
                instance.EnableFormatting = bool.Parse(config["format:enable"]);
                if (Enum.TryParse(config["trace"], out Trace value))
                {
                    instance.Trace = value;
                }

                return instance;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to obtain configuration from the client: {ex.Message}");
                return null;
            }
        }
    }
}
