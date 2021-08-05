// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal class RazorCodeActionParams : CodeActionParams
    {
        public new ExtendedCodeActionContext Context { get; set; }
    }
}
