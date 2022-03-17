// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal sealed class RegistrationExtensionResult
    {
        public RegistrationExtensionResult(string serverCapability!!, object options!!)
        {
            ServerCapability = serverCapability;
            Options = options;
        }

        public string ServerCapability { get; }

        public object Options { get; }
    }
}
