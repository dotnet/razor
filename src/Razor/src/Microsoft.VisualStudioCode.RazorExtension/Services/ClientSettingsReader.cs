// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.CodeAnalysis.Razor.Workspaces.Settings;

namespace Microsoft.VisualStudioCode.RazorExtension.Configuration;

[Shared]
[Export(typeof(IClientSettingsReader))]
[Export(typeof(ClientSettingsReader))]
internal class ClientSettingsReader : IClientSettingsReader
{
    private ClientSettings _currentSettings = ClientSettings.Default;

    public ClientSettings GetClientSettings()
    {
        return _currentSettings;
    }

    public void Update(ClientAdvancedSettings updateSettings)
    {
        _currentSettings = _currentSettings with
        {
            AdvancedSettings = updateSettings
        };
    }
}
