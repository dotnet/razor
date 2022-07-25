// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal sealed class ExtractToCodeBehindCodeActionParams
    {
        public Uri Uri { get; set; }
        public int ExtractStart { get; set; }
        public int ExtractEnd { get; set; }
        public int RemoveStart { get; set; }
        public int RemoveEnd { get; set; }
        public string Namespace { get; set; }
    }
}
