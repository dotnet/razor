// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class TextEditExtensions
    {
        public static TextChange AsTextChange(this TextEdit textEdit!!, SourceText sourceText!!)
        {
            var span = textEdit.Range.AsTextSpan(sourceText);
            return new TextChange(span, textEdit.NewText);
        }
    }
}
