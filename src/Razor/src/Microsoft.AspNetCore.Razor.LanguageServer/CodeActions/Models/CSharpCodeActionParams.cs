// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal sealed class CSharpCodeActionParams
    {
        public object Data { get; set; }
        public DocumentUri RazorFileUri { get; set; }
    }
}
