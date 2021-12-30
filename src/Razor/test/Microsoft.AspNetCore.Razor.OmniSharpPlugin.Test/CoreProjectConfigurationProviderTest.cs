﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Xunit;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin
{
    public class CoreProjectConfigurationProviderTest : OmniSharpTestBase
    {
        [Fact]
        public void HasRazorCoreCapability_NoCapabilities_ReturnsFalse()
        {
            // Arrange
            var projectCapabilities = Array.Empty<string>();
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            var context = new ProjectConfigurationProviderContext(projectCapabilities, projectInstance);
            var provider = new TestCoreProjectConfigurationProvider();

            // Act
            var result = provider.HasRazorCoreCapability(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasRazorCoreCapability_DotNetCoreCapability_ReturnsTrue()
        {
            // Arrange
            var projectCapabilities = new[] { CoreProjectConfigurationProvider.DotNetCoreRazorCapability };
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            var context = new ProjectConfigurationProviderContext(projectCapabilities, projectInstance);
            var provider = new TestCoreProjectConfigurationProvider();

            // Act
            var result = provider.HasRazorCoreCapability(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasRazorCoreCapability_DotNetCoreWebCapability_ReturnsTrue()
        {
            // Arrange
            var projectCapabilities = new[] { CoreProjectConfigurationProvider.DotNetCoreWebCapability };
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            var context = new ProjectConfigurationProviderContext(projectCapabilities, projectInstance);
            var provider = new TestCoreProjectConfigurationProvider();

            // Act
            var result = provider.HasRazorCoreCapability(context);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasRazorCoreConfigurationCapability_CoreRazorCapabilities_ReturnsFalse()
        {
            // Arrange
            var projectCapabilities = new[] { CoreProjectConfigurationProvider.DotNetCoreRazorCapability };
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            var context = new ProjectConfigurationProviderContext(projectCapabilities, projectInstance);
            var provider = new TestCoreProjectConfigurationProvider();

            // Act
            var result = provider.HasRazorCoreConfigurationCapability(context);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasRazorCoreConfigurationCapability_DotNetCoreRazorConfigCapability_ReturnsTrue()
        {
            // Arrange
            var projectCapabilities = new[] { CoreProjectConfigurationProvider.DotNetCoreRazorConfigurationCapability };
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            var context = new ProjectConfigurationProviderContext(projectCapabilities, projectInstance);
            var provider = new TestCoreProjectConfigurationProvider();

            // Act
            var result = provider.HasRazorCoreConfigurationCapability(context);

            // Assert
            Assert.True(result);
        }

        private class TestCoreProjectConfigurationProvider : CoreProjectConfigurationProvider
        {
            public new bool HasRazorCoreCapability(ProjectConfigurationProviderContext context) => base.HasRazorCoreCapability(context);

            public new bool HasRazorCoreConfigurationCapability(ProjectConfigurationProviderContext context) => base.HasRazorCoreConfigurationCapability(context);

            public override bool TryResolveConfiguration(ProjectConfigurationProviderContext context, out ProjectConfiguration configuration)
            {
                throw new NotImplementedException();
            }
        }
    }
}
