// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class RazorConfigurationEndpointTest : LanguageServerTestBase
    {
        public RazorConfigurationEndpointTest()
        {
            var services = new ServiceCollection().AddOptions();
            Cache = services.BuildServiceProvider().GetRequiredService<IOptionsMonitorCache<RazorLSPOptions>>();

            ConfigurationService = Mock.Of<RazorConfigurationService>(MockBehavior.Strict);
        }

        private IOptionsMonitorCache<RazorLSPOptions> Cache { get; }

        private RazorConfigurationService ConfigurationService { get; }

        [Fact]
        public async Task Handle_UpdatesOptions()
        {
            // Arrange
            var optionsMonitor = new TestRazorLSPOptionsMonitor(ConfigurationService, Cache);
            var endpoint = new RazorConfigurationEndpoint(optionsMonitor, LoggerFactory);
            var request = new DidChangeConfigurationParamsBridge();

            // Act
            await endpoint.Handle(request, CancellationToken.None);

            // Assert
            Assert.True(optionsMonitor.Called, "UpdateAsync was not called.");
        }

        private class TestRazorLSPOptionsMonitor : RazorLSPOptionsMonitor
        {
            public TestRazorLSPOptionsMonitor(RazorConfigurationService configurationService, IOptionsMonitorCache<RazorLSPOptions> cache) : base(configurationService, cache)
            {
            }

            public bool Called { get; private set; }

            public override Task UpdateAsync(CancellationToken cancellationToken)
            {
                Called = true;
                return Task.CompletedTask;
            }
        }
    }
}
