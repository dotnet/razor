// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    internal class SerializedRazorExtension : RazorExtension
    {
        public SerializedRazorExtension(string extensionName!!)
        {
            ExtensionName = extensionName;
        }

        public override string ExtensionName { get; }
    }
}
