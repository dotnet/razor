// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.Editor.Razor.Settings;

internal interface IClientSettingsManager
{
    ClientSettings ClientSettings { get; }
    event EventHandler<ClientSettingsChangedEventArgs> ClientSettingsChanged;

    void Update(ClientSpaceSettings updateSettings);
    void Update(ClientCompletionSettings updateSettings);
    void Update(ClientAdvancedSettings updateSettings);
    bool IsLogLevelEnabled(LogLevel logLevel);

}
