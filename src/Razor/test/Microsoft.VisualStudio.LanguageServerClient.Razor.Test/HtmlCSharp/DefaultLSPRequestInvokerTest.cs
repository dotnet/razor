﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class DefaultLSPRequestInvokerTest
    {
        private FallbackCapabilitiesFilterResolver CapabilitiesResolver => new DefaultFallbackCapabilitiesFilterResolver();

        [Fact]
        public async Task ReinvokeRequestOnServerAsync_InvokesRazorLanguageClient()
        {
            // Arrange
            var called = false;
            var expectedMethod = "razor/test";
            var broker = new TestLanguageServiceBroker((method) =>
            {
                called = true;
                Assert.Equal(expectedMethod, method);
            });
            var requestInvoker = new DefaultLSPRequestInvoker(broker, CapabilitiesResolver);

            // Act
            await requestInvoker.ReinvokeRequestOnServerAsync<object, object>(expectedMethod, RazorLSPConstants.RazorLanguageServerName, new object(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task ReinvokeRequestOnServerAsync_InvokesHtmlLanguageClient()
        {
            // Arrange
            var called = false;
            var expectedMethod = "textDocument/test";
            var broker = new TestLanguageServiceBroker((method) =>
            {
                called = true;
                Assert.Equal(expectedMethod, method);
            });
            var requestInvoker = new DefaultLSPRequestInvoker(broker, CapabilitiesResolver);

            // Act
            await requestInvoker.ReinvokeRequestOnServerAsync<object, object>(expectedMethod, RazorLSPConstants.HtmlLanguageServerName, new object(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task ReinvokeRequestOnServerAsync_InvokesCSharpLanguageClient()
        {
            // Arrange
            var called = false;
            var expectedMethod = "textDocument/test";
            var broker = new TestLanguageServiceBroker((method) =>
            {
                called = true;
                Assert.Equal(expectedMethod, method);
            });
            var requestInvoker = new DefaultLSPRequestInvoker(broker, CapabilitiesResolver);

            // Act
            await requestInvoker.ReinvokeRequestOnServerAsync<object, object>(expectedMethod, RazorLSPConstants.RazorCSharpLanguageServerName, new object(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task CustomRequestServerAsync_InvokesRazorLanguageClient()
        {
            // Arrange
            var called = false;
            var expectedMethod = "razor/test";
            var broker = new TestLanguageServiceBroker((method) =>
            {
                called = true;
                Assert.Equal(expectedMethod, method);
            });
            var requestInvoker = new DefaultLSPRequestInvoker(broker, CapabilitiesResolver);

            // Act
            await requestInvoker.ReinvokeRequestOnServerAsync<object, object>(expectedMethod, RazorLSPConstants.RazorLanguageServerName, new object(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task CustomRequestServerAsync_InvokesHtmlLanguageClient()
        {
            // Arrange
            var called = false;
            var expectedMethod = "textDocument/test";
            var broker = new TestLanguageServiceBroker((method) =>
            {
                called = true;
                Assert.Equal(expectedMethod, method);
            });
            var requestInvoker = new DefaultLSPRequestInvoker(broker, CapabilitiesResolver);

            // Act
            await requestInvoker.ReinvokeRequestOnServerAsync<object, object>(expectedMethod, RazorLSPConstants.HtmlLanguageServerName, new object(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task CustomRequestServerAsync_InvokesCSharpLanguageClient()
        {
            // Arrange
            var called = false;
            var expectedMethod = "textDocument/test";
            var broker = new TestLanguageServiceBroker((method) =>
            {
                called = true;
                Assert.Equal(expectedMethod, method);
            });
            var requestInvoker = new DefaultLSPRequestInvoker(broker, CapabilitiesResolver);

            // Act
            await requestInvoker.ReinvokeRequestOnServerAsync<object, object>(expectedMethod, RazorLSPConstants.RazorCSharpLanguageServerName, new object(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
        }
    }
}
