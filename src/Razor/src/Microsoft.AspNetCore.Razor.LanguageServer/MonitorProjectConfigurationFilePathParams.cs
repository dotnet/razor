// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using MediatR;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class MonitorProjectConfigurationFilePathParams : IRequest
    {
        public string ProjectFilePath { get; set; }

        public string ConfigurationFilePath { get; set; }
    }
}
