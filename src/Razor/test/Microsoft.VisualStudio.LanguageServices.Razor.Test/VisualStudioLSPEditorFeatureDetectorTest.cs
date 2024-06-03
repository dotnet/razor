// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.Razor.Logging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor;

public class VisualStudioLSPEditorFeatureDetectorTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [UIFact]
    public void IsLspEditorAvailable_FeatureFlagEnabled_ReturnsFalse()
    {
        // Arrange
        var featureFlagService = CreateFeatureFlagService(useLegacyRazorEditor: true);
        var settingsPersistenceService = CreateSettingsPersistenceService();
        var uiContextService = CreateUIContextService();
        using var activityLog = CreateActivityLog();

        var featureDetector = new LspEditorFeatureDetector(featureFlagService, settingsPersistenceService, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.False(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_FeatureFlagDisabled_ReturnsTrue()
    {
        // Arrange
        var featureFlagService = CreateFeatureFlagService(useLegacyRazorEditor: false);
        var settingsPersistenceService = CreateSettingsPersistenceService();
        var uiContextService = CreateUIContextService();
        using var activityLog = CreateActivityLog();

        var featureDetector = new LspEditorFeatureDetector(featureFlagService, settingsPersistenceService, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_OptionEnabled_ReturnsFalse()
    {
        // Arrange
        var featureFlagService = CreateFeatureFlagService();
        var settingsPersistenceService = CreateSettingsPersistenceService(useLegacyRazorEditor: true);
        var uiContextService = CreateUIContextService();
        using var activityLog = CreateActivityLog();

        var featureDetector = new LspEditorFeatureDetector(featureFlagService, settingsPersistenceService, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.False(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_OptionDisabled_ReturnsTrue()
    {
        // Arrange
        var featureFlagService = CreateFeatureFlagService();
        var settingsPersistenceService = CreateSettingsPersistenceService(useLegacyRazorEditor: false);
        var uiContextService = CreateUIContextService();
        using var activityLog = CreateActivityLog();

        var featureDetector = new LspEditorFeatureDetector(featureFlagService, settingsPersistenceService, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_CloudEnvironmentConnected_ReturnsTrue()
    {
        // Arrange
        var featureFlagService = CreateFeatureFlagService();
        var settingsPersistenceService = CreateSettingsPersistenceService();
        var uiContextService = CreateUIContextService(cloudEnvironmentConnectedActive: true);
        using var activityLog = CreateActivityLog();
        var featureDetector = new LspEditorFeatureDetector(featureFlagService, settingsPersistenceService, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsRemoteClient_CloudEnvironmentConnected_ReturnsTrue()
    {
        // Arrange
        var featureFlagService = CreateFeatureFlagService();
        var settingsPersistenceService = CreateSettingsPersistenceService();
        var uiContextService = CreateUIContextService(cloudEnvironmentConnectedActive: true);
        using var activityLog = CreateActivityLog();
        var featureDetector = new LspEditorFeatureDetector(featureFlagService, settingsPersistenceService, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsRemoteClient_LiveShareGuest_ReturnsTrue()
    {
        // Arrange
        var featureFlagService = CreateFeatureFlagService();
        var settingsPersistenceService = CreateSettingsPersistenceService();
        var uiContextService = CreateUIContextService(isLiveShareGuestActive: true);
        using var activityLog = CreateActivityLog();
        var featureDetector = new LspEditorFeatureDetector(featureFlagService, settingsPersistenceService, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsRemoteClient_UnknownEnvironment_ReturnsFalse()
    {
        // Arrange
        var featureFlagService = CreateFeatureFlagService();
        var settingsPersistenceService = CreateSettingsPersistenceService();
        var uiContextService = CreateUIContextService();
        using var activityLog = CreateActivityLog();
        var featureDetector = new LspEditorFeatureDetector(featureFlagService, settingsPersistenceService, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.False(result);
    }

    private RazorActivityLog CreateActivityLog()
    {
        var vsActivityLogMock = new StrictMock<IVsActivityLog>();
        vsActivityLogMock
            .Setup(x => x.LogEntry(It.IsAny<uint>(), "Razor", It.IsAny<string>()))
            .Returns(VSConstants.S_OK);

        var serviceProviderMock = new StrictMock<IAsyncServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetServiceAsync(typeof(SVsActivityLog)))
            .ReturnsAsync(vsActivityLogMock.Object);

        return new RazorActivityLog(serviceProviderMock.Object, JoinableTaskContext);
    }

    private IFeatureFlagService CreateFeatureFlagService(bool useLegacyRazorEditor = false)
    {
        var mock = new StrictMock<IFeatureFlagService>();
        mock.Setup(x => x.IsFeatureEnabled(WellKnownFeatureFlagNames.UseLegacyRazorEditor, It.IsAny<bool>()))
            .Returns(useLegacyRazorEditor);

        return mock.Object;
    }

    private ISettingsPersistenceService CreateSettingsPersistenceService(bool useLegacyRazorEditor = false)
    {
        var mock = new StrictMock<ISettingsPersistenceService>();
        mock.Setup(x => x.GetValueOrDefault(WellKnownSettingNames.UseLegacyASPNETCoreEditor, It.IsAny<bool>()))
            .Returns(useLegacyRazorEditor);

        return mock.Object;
    }

    private IUIContextService CreateUIContextService(
        bool isLiveShareHostActive = false,
        bool isLiveShareGuestActive = false,
        bool cloudEnvironmentConnectedActive = false)
    {
        var mock = new StrictMock<IUIContextService>();

        mock.Setup(x => x.IsActive(Guids.LiveShareHostUIContextGuid))
            .Returns(isLiveShareHostActive);

        mock.Setup(x => x.IsActive(Guids.LiveShareGuestUIContextGuid))
            .Returns(isLiveShareGuestActive);

        mock.Setup(x => x.IsActive(VSConstants.UICONTEXT.CloudEnvironmentConnected_guid))
            .Returns(cloudEnvironmentConnectedActive);

        return mock.Object;
    }
}
