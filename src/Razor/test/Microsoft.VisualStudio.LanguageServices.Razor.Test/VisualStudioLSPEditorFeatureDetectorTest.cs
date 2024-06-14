// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Razor.Logging;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor;

public class VisualStudioLSPEditorFeatureDetectorTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [UITheory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void IsLspEditorAvailable(bool legacyEditorFeatureFlag, bool legacyEditorSetting, bool expected)
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector(legacyEditorFeatureFlag, legacyEditorSetting);

        // Act
        var result = featureDetector.IsLSPEditorAvailable();

        // Assert
        Assert.Equal(expected, result);
    }

    [UIFact]
    public void IsLSPEditorAvailable_IsVSRemoteClient_ReturnsTrue()
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector();

        // Act
        var result = featureDetector.IsLSPEditorAvailable();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsLSPEditorAvailable_UnsupportedProject_ReturnsFalse()
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector();

        // Act
        var result = featureDetector.IsLSPEditorAvailable();

        // Assert
        Assert.False(result);
    }

    [UIFact]
    public void IsRemoteClient_VSRemoteClient_ReturnsTrue()
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector();

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsRemoteClient_LiveShareGuest_ReturnsTrue()
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector();

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsRemoteClient_UnknownEnvironment_ReturnsFalse()
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector();

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.False(result);
    }

    private ILspEditorFeatureDetector CreateLspEditorFeatureDetector(bool featureFlagEnabled = false, bool useLegacyEditorSetting = false)
    {
        var featureDetector = new VisualStudioLSPEditorFeatureDetector(
            CreateVsFeatureFlagsService(featureFlagEnabled),
            CreateVsSettingsManagerService(useLegacyEditorSetting),
            JoinableTaskContext,
            CreateRazorActivityLog());

        AddDisposable(featureDetector);

        return featureDetector;
    }

    private static IVsService<SVsFeatureFlags, IVsFeatureFlags> CreateVsFeatureFlagsService(bool useLegacyEditor)
    {
        var vsFeatureFlagsMock = new StrictMock<IVsFeatureFlags>();
        vsFeatureFlagsMock
            .Setup(x => x.IsFeatureEnabled(WellKnownFeatureFlagNames.UseLegacyRazorEditor, It.IsAny<bool>()))
            .Returns(useLegacyEditor);

        var vsFeatureFlagsServiceMock = new StrictMock<IVsService<SVsFeatureFlags, IVsFeatureFlags>>();
        vsFeatureFlagsServiceMock
            .Setup(x => x.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(vsFeatureFlagsMock.Object);

        return vsFeatureFlagsServiceMock.Object;
    }

    private static IVsService<SVsSettingsPersistenceManager, ISettingsManager> CreateVsSettingsManagerService(bool useLegacyEditor)
    {
        var vsSettingsManagerMock = new StrictMock<ISettingsManager>();
        vsSettingsManagerMock
            .Setup(x => x.GetValueOrDefault(WellKnownSettingNames.UseLegacyASPNETCoreEditor, It.IsAny<bool>()))
            .Returns(useLegacyEditor);

        var vsSettingsManagerServiceMock = new StrictMock<IVsService<SVsSettingsPersistenceManager, ISettingsManager>>();
        vsSettingsManagerServiceMock
            .Setup(x => x.GetValueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(vsSettingsManagerMock.Object);

        return vsSettingsManagerServiceMock.Object;
    }

    private RazorActivityLog CreateRazorActivityLog()
    {
        var vsActivityLogMock = new StrictMock<IVsActivityLog>();
        vsActivityLogMock
            .Setup(x => x.LogEntry(It.IsAny<uint>(), "Razor", It.IsAny<string>()))
            .Callback((uint entryType, string source, string description) =>
            {
                switch ((__ACTIVITYLOG_ENTRYTYPE)entryType)
                {
                    case __ACTIVITYLOG_ENTRYTYPE.ALE_ERROR:
                        Logger.LogError($"Error:{description}");
                        break;

                    case __ACTIVITYLOG_ENTRYTYPE.ALE_WARNING:
                        Logger.LogError($"Warning:{description}");
                        break;

                    case __ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION:
                        Logger.LogError($"Info:{description}");
                        break;

                    default:
                        Assumed.Unreachable();
                        break;
                }
            })
            .Returns(VSConstants.S_OK);

        var serviceProviderMock = new StrictMock<IAsyncServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetServiceAsync(typeof(SVsActivityLog)))
            .ReturnsAsync(vsActivityLogMock.Object);

        var activityLog = new RazorActivityLog(serviceProviderMock.Object, JoinableTaskContext);

        AddDisposable(activityLog);

        return activityLog;
    }
}
