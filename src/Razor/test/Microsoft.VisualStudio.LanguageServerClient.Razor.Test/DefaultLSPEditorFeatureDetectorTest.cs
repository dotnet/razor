// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.VisualStudio.Shell.Interop;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    public class DefaultLSPEditorFeatureDetectorTest
    {
        [Fact]
        public void IsLSPEditorAvailable_ProjectSupported_ReturnsTrue()
        {
            // Arrange
            var featureDetector = new TestLSPEditorFeatureDetector()
            {
                ProjectSupportsLSPEditorValue = true,
            };

            // Act
            var result = featureDetector.IsLSPEditorAvailable("testMoniker", hierarchy: null);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsLSPEditorAvailable_LegacyEditorEnabled_ReturnsFalse()
        {
            // Arrange
            var featureDetector = new TestLSPEditorFeatureDetector()
            {
                UseLegacyEditor = true,
                ProjectSupportsLSPEditorValue = true,
            };

            // Act
            var result = featureDetector.IsLSPEditorAvailable("testMoniker", hierarchy: null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsLSPEditorAvailable_IsVSRemoteClient_ReturnsTrue()
        {
            // Arrange
            var featureDetector = new TestLSPEditorFeatureDetector()
            {
                IsVSRemoteClientValue = true,
                ProjectSupportsLSPEditorValue = true,
            };

            // Act
            var result = featureDetector.IsLSPEditorAvailable("testMoniker", hierarchy: null);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsLSPEditorAvailable_UnsupportedProject_ReturnsFalse()
        {
            // Arrange
            var featureDetector = new TestLSPEditorFeatureDetector()
            {
                ProjectSupportsLSPEditorValue = false,
            };

            // Act
            var result = featureDetector.IsLSPEditorAvailable("testMoniker", hierarchy: null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsRemoteClient_VSRemoteClient_ReturnsTrue()
        {
            // Arrange
            var featureDetector = new TestLSPEditorFeatureDetector()
            {
                IsVSRemoteClientValue = true,
            };

            // Act
            var result = featureDetector.IsRemoteClient();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsRemoteClient_LiveShareGuest_ReturnsTrue()
        {
            // Arrange
            var featureDetector = new TestLSPEditorFeatureDetector()
            {
                IsLiveShareGuestValue = true,
            };

            // Act
            var result = featureDetector.IsRemoteClient();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsRemoteClient_UnknownEnvironment_ReturnsFalse()
        {
            // Arrange
            var featureDetector = new TestLSPEditorFeatureDetector();

            // Act
            var result = featureDetector.IsRemoteClient();

            // Assert
            Assert.False(result);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        private class TestLSPEditorFeatureDetector : DefaultLSPEditorFeatureDetector
        {
            public bool UseLegacyEditor { get; set; }

            public bool IsLiveShareGuestValue { get; set; }

            public bool IsLiveShareHostValue { get; set; }

            public bool IsVSRemoteClientValue { get; set; }

            public bool ProjectSupportsLSPEditorValue { get; set; }

            public override bool IsLSPEditorAvailable() => !UseLegacyEditor;

            public override bool IsLiveShareHost() => IsLiveShareHostValue;

            private protected override bool IsLiveShareGuest() => IsLiveShareGuestValue;

            private protected override bool IsVSRemoteClient() => IsVSRemoteClientValue;

            private protected override bool ProjectSupportsLSPEditor(string documentMoniker, IVsHierarchy hierarchy) => ProjectSupportsLSPEditorValue;
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
