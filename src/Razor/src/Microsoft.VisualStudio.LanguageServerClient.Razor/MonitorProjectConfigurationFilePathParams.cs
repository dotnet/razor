// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal class MonitorProjectConfigurationFilePathParams
    {
        public string ProjectFilePath { get; set; }

        public string ConfigurationFilePath { get; set; }
    }
}
