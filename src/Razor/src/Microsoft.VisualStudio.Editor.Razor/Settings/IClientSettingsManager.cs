// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.Editor.Razor.Settings;

internal interface IClientSettingsManager
{
    bool IsFeedbackBeingRecorded { get; }
    event EventHandler<bool> FeedbackRecordingChanged;

    ClientSettings ClientSettings { get; }
    event EventHandler<ClientSettingsChangedEventArgs> ClientSettingsChanged;

    void Update(ClientSpaceSettings updateSettings);
    void Update(ClientCompletionSettings updateSettings);
    void Update(ClientAdvancedSettings updateSettings);

    /// <summary>
    /// Returns true if <see cref="IsFeedbackBeingRecorded"/> or if the level is >= <see cref="ClientAdvancedSettings.LogLevel"/>
    /// </summary>
    bool ShouldLog(LogLevel logLevel);
}
