// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.Language
{
    internal class DefaultAllowedChildTagDescriptor : AllowedChildTagDescriptor
    {
        public DefaultAllowedChildTagDescriptor(string name, string displayName, bool caseSensitive, RazorDiagnostic[] diagnostics)
        {
            Name = name;
            DisplayName = displayName;
            CaseSensitive = caseSensitive;
            Diagnostics = diagnostics;
        }
    }
}
