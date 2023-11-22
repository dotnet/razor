// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.Editor.Razor.Logging;
using Microsoft.VisualStudio.Shell.Interop;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

public class VisualStudioWindowsLSPEditorFeatureDetectorTest : ToolingTestBase
{
    public VisualStudioWindowsLSPEditorFeatureDetectorTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public void IsLSPEditorAvailable_ProjectSupported_ReturnsTrue()
    {
        // Arrange
        var featureDetector = new TestLSPEditorFeatureDetector(TestOutputWindowLogger.Instance)
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
        var featureDetector = new TestLSPEditorFeatureDetector(TestOutputWindowLogger.Instance)
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
        var featureDetector = new TestLSPEditorFeatureDetector(TestOutputWindowLogger.Instance)
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
        var featureDetector = new TestLSPEditorFeatureDetector(TestOutputWindowLogger.Instance)
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
        var featureDetector = new TestLSPEditorFeatureDetector(TestOutputWindowLogger.Instance)
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
        var featureDetector = new TestLSPEditorFeatureDetector(TestOutputWindowLogger.Instance)
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
        var featureDetector = new TestLSPEditorFeatureDetector(TestOutputWindowLogger.Instance);

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.False(result);
    }

#pragma warning disable CS0618 // Type or member is obsolete (Test constructor)
    private class TestLSPEditorFeatureDetector : VisualStudioWindowsLSPEditorFeatureDetector
    {
        public TestLSPEditorFeatureDetector(IOutputWindowLogger logger)
            : base(projectCapabilityResolver: null, logger)
        {
        }

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
#pragma warning restore CS0618 // Type or member is obsolete (Test constructor)
}
