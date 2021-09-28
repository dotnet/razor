﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class RazorLSPOptionsMonitorTest
    {
        public RazorLSPOptionsMonitorTest()
        {
            var services = new ServiceCollection().AddOptions();
            Cache = services.BuildServiceProvider().GetRequiredService<IOptionsMonitorCache<RazorLSPOptions>>();
        }

        private IOptionsMonitorCache<RazorLSPOptions> Cache { get; }

        [Fact]
        public async Task UpdateAsync_Invokes_OnChangeRegistration()
        {
            // Arrange
            var expectedOptions = new RazorLSPOptions(Trace.Messages, enableFormatting: false, autoClosingTags: true, insertSpaces: true, tabSize: 4);
            var configService = Mock.Of<RazorConfigurationService>(f => f.GetLatestOptionsAsync(CancellationToken.None) == Task.FromResult(expectedOptions), MockBehavior.Strict);
            var optionsMonitor = new RazorLSPOptionsMonitor(configService, Cache);
            var called = false;

            // Act & Assert
            optionsMonitor.OnChange(options =>
            {
                called = true;
                Assert.Same(expectedOptions, options);
            });
            await optionsMonitor.UpdateAsync(CancellationToken.None);
            Assert.True(called, "Registered callback was not called.");
        }

        [Fact]
        public async Task UpdateAsync_DoesNotInvoke_OnChangeRegistration_AfterDispose()
        {
            // Arrange
            var expectedOptions = new RazorLSPOptions(Trace.Messages, enableFormatting: false, autoClosingTags: true, insertSpaces: true, tabSize: 4);
            var configService = Mock.Of<RazorConfigurationService>(f => f.GetLatestOptionsAsync(CancellationToken.None) == Task.FromResult(expectedOptions), MockBehavior.Strict);
            var optionsMonitor = new RazorLSPOptionsMonitor(configService, Cache);
            var called = false;
            var onChangeToken = optionsMonitor.OnChange(options => called = true);

            // Act 1
            await optionsMonitor.UpdateAsync(CancellationToken.None);

            // Assert 1
            Assert.True(called, "Registered callback was not called.");

            // Act 2
            called = false;
            onChangeToken.Dispose();
            await optionsMonitor.UpdateAsync(CancellationToken.None);

            // Assert 2
            Assert.False(called, "Registered callback called even after dispose.");
        }

        [Fact]
        public async Task UpdateAsync_ConfigReturnsNull_DoesNotInvoke_OnChangeRegistration()
        {
            // Arrange
            var configService = new Mock<RazorConfigurationService>(MockBehavior.Strict).Object;
            Mock.Get(configService).Setup(s => s.GetLatestOptionsAsync(CancellationToken.None)).ReturnsAsync(value: null);
            var optionsMonitor = new RazorLSPOptionsMonitor(configService, Cache);
            var called = false;
            var onChangeToken = optionsMonitor.OnChange(options => called = true);

            // Act
            await optionsMonitor.UpdateAsync(CancellationToken.None);

            // Assert
            Assert.False(called, "Registered callback called even when GetLatestOptionsAsync() returns null.");
        }
    }
}
