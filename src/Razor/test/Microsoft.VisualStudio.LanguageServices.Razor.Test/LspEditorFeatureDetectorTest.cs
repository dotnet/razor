// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;
using Microsoft.VisualStudio.Razor.Logging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor;

public class LspEditorFeatureDetectorTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [UIFact]
    public void IsLspEditorAvailable_FeatureFlagEnabled_ReturnsFalse()
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector(featureFlagEnabled: true);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.False(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_FeatureFlagDisabled_ReturnsTrue()
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector(featureFlagEnabled: false);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.True(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_OptionEnabled_ReturnsFalse()
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector(settingEnabled: true);

        // Act
        var result = featureDetector.IsLspEditorAvailable();

        // Assert
        Assert.False(result);
    }

    [UIFact]
    public void IsLspEditorAvailable_OptionDisabled_ReturnsTrue()
    {
        // Arrange
        var featureDetector = CreateLspEditorFeatureDetector(settingEnabled: false);

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
        var featureDetector = CreateLspEditorFeatureDetector(uiContextService);

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
        var featureDetector = CreateLspEditorFeatureDetector(uiContextService);

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
        var featureDetector = CreateLspEditorFeatureDetector(uiContextService);

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
        var featureDetector = CreateLspEditorFeatureDetector(uiContextService);

        // Act
        var result = featureDetector.IsRemoteClient();

        // Assert
        Assert.False(result);
    }

    private LspEditorFeatureDetector CreateLspEditorFeatureDetector(IUIContextService uiContextService)
        => CreateLspEditorFeatureDetector(featureFlagEnabled: false, settingEnabled: false, uiContextService);

    private LspEditorFeatureDetector CreateLspEditorFeatureDetector(
        bool featureFlagEnabled = false,
        bool settingEnabled = false,
        IUIContextService? uiContextService = null)
    {
        uiContextService ??= CreateUIContextService();

        var visualStudioOptionsProvider = CreateVisualStudioOptionsProvider(featureFlagEnabled, settingEnabled);

        var serviceProvider = VsMocks.CreateServiceProvider(builder =>
        {
            builder.AddService(visualStudioOptionsProvider);
        });

        var activityLog = CreateActivityLog();
        AddDisposable(activityLog);

        return new(serviceProvider, uiContextService, activityLog);
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

    private static IVisualStudioOptionsProvider CreateVisualStudioOptionsProvider(bool featureFlagEnabled = false, bool settingEnabled = false)
    {
        var mock = new StrictMock<IVisualStudioOptionsProvider>();

        mock.Setup(x => x.IsFeatureEnabled(WellKnownFeatureFlagNames.UseLegacyRazorEditor, It.IsAny<bool>()))
            .Returns(featureFlagEnabled);

        mock.Setup(x => x.GetValueOrDefault(WellKnownSettingNames.UseLegacyASPNETCoreEditor, It.IsAny<bool>()))
            .Returns(settingEnabled);

        return mock.Object;
    }

    private static IUIContextService CreateUIContextService(
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
