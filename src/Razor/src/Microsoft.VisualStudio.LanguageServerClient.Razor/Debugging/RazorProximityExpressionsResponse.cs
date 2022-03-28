// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Debugging
{
    internal class RazorProximityExpressionsResponse
    {
        public IReadOnlyList<string> Expressions { get; set; }
    }
}
