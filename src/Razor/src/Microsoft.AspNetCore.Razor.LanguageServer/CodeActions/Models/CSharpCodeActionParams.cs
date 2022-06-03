// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal sealed class CSharpCodeActionParams
    {
        public object Data { get; set; }
        public Uri RazorFileUri { get; set; }
    }
}
