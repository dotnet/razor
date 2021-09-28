// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal sealed class CreateComponentCodeActionParams
    {
        public DocumentUri Uri { get; set; }
        public string Path { get; set; }
    }
}
