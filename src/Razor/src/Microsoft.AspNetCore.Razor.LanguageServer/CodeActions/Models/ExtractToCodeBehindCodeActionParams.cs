// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal sealed class ExtractToCodeBehindCodeActionParams
    {
        public DocumentUri Uri { get; set; }
        public int ExtractStart { get; set; }
        public int ExtractEnd { get; set; }
        public int RemoveStart { get; set; }
        public int RemoveEnd { get; set; }
    }
}
