// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Test.Common;
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
    [UIFact]
    public void IsLspEditorAvailable_FeatureFlagEnabled_ReturnsFalse()
    {
        // Arrange
        var uiContextService = CreateUIContextService();
        var serviceProvider = CreateServiceProvider(featureFlagEnabled: true);
        using var activityLog = CreateActivityLog();

        var featureDetector = new LspEditorFeatureDetector(serviceProvider, JoinableTaskContext, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.False(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_FeatureFlagDisabled_ReturnsTrue()
    {
        // Arrange
        var uiContextService = CreateUIContextService();
        var serviceProvider = CreateServiceProvider(featureFlagEnabled: false);
        using var activityLog = CreateActivityLog();

        var featureDetector = new LspEditorFeatureDetector(serviceProvider, JoinableTaskContext, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_OptionEnabled_ReturnsFalse()
    {
        // Arrange
        var uiContextService = CreateUIContextService();
        var serviceProvider = CreateServiceProvider(optionEnabled: true);
        using var activityLog = CreateActivityLog();

        var featureDetector = new LspEditorFeatureDetector(serviceProvider, JoinableTaskContext, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.False(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_OptionDisabled_ReturnsTrue()
    {
        // Arrange
        var uiContextService = CreateUIContextService();
        var serviceProvider = CreateServiceProvider(optionEnabled: false);
        using var activityLog = CreateActivityLog();

        var featureDetector = new LspEditorFeatureDetector(serviceProvider, JoinableTaskContext, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_CloudEnvironmentConnected_ReturnsTrue()
    {
        // Arrange
        var uiContextService = CreateUIContextService(cloudEnvironmentConnectedActive: true);
        var serviceProvider = CreateServiceProvider();
        using var activityLog = CreateActivityLog();
        var featureDetector = new LspEditorFeatureDetector(serviceProvider, JoinableTaskContext, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsRemoteClient_CloudEnvironmentConnected_ReturnsTrue()
    {
        // Arrange
        var uiContextService = CreateUIContextService(cloudEnvironmentConnectedActive: true);
        var serviceProvider = CreateServiceProvider();
        using var activityLog = CreateActivityLog();
        var featureDetector = new LspEditorFeatureDetector(serviceProvider, JoinableTaskContext, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsRemoteClient_LiveShareGuest_ReturnsTrue()
    {
        // Arrange
        var uiContextService = CreateUIContextService(isLiveShareGuestActive: true);
        var serviceProvider = CreateServiceProvider();
        using var activityLog = CreateActivityLog();
        var featureDetector = new LspEditorFeatureDetector(serviceProvider, JoinableTaskContext, uiContextService, activityLog);

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsRemoteClient_UnknownEnvironment_ReturnsFalse()
    {
        // Arrange
        var uiContextService = CreateUIContextService();
        var serviceProvider = CreateServiceProvider();
        using var activityLog = CreateActivityLog();
        var featureDetector = new LspEditorFeatureDetector(serviceProvider, JoinableTaskContext, uiContextService, activityLog);

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

    private IAsyncServiceProvider CreateServiceProvider(bool featureFlagEnabled = false, bool optionEnabled = false)
    {
        var featureFlagsMock = new StrictMock<IVsFeatureFlags>();
        featureFlagsMock
            .Setup(x => x.IsFeatureEnabled(WellKnownFeatureFlagNames.UseLegacyRazorEditor, It.IsAny<bool>()))
            .Returns(featureFlagEnabled);

        var settingsManagerMock = new StrictMock<ISettingsManager>();
        settingsManagerMock
            .Setup(x => x.GetValueOrDefault(WellKnownSettingNames.UseLegacyASPNETCoreEditor, It.IsAny<bool>()))
            .Returns(optionEnabled);

        var serviceProviderMock = new StrictMock<IAsyncServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetServiceAsync(typeof(SVsFeatureFlags)))
            .ReturnsAsync(featureFlagsMock.Object);
        serviceProviderMock
            .Setup(x => x.GetServiceAsync(typeof(SVsSettingsPersistenceManager)))
            .ReturnsAsync(settingsManagerMock.Object);

        return serviceProviderMock.Object;
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
