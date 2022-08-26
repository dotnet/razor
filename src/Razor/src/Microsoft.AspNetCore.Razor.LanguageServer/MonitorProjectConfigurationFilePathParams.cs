// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class MonitorProjectConfigurationFilePathParams : IRequest
    {
        public required string ProjectFilePath { get; set; }

        public required string ConfigurationFilePath { get; set; }
    }
}
