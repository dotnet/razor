// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Text;
using Omni = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class TextEditExtensions
    {
        public static TextChange AsTextChange(this Omni.TextEdit textEdit, SourceText sourceText)
        {
            if (textEdit is null)
            {
                throw new ArgumentNullException(nameof(textEdit));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            var span = textEdit.Range.AsTextSpan(sourceText);
            return new TextChange(span, textEdit.NewText);
        }

        public static TextChange AsTextChange(this VS.TextEdit textEdit, SourceText sourceText)
        {
            if (textEdit is null)
            {
                throw new ArgumentNullException(nameof(textEdit));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            var span = textEdit.Range.AsTextSpan(sourceText);
            return new TextChange(span, textEdit.NewText);
        }

        public static VS.TextEdit AsVSTextEdit(this Omni.TextEdit textEdit)
        {
            return new VS.TextEdit
            {
                NewText = textEdit.NewText,
                Range = textEdit.Range.AsVSRange(),
            };
        }

        public static Omni.TextEdit AsOmniSharpTextEdit(this VS.TextEdit textEdit)
        {
            return new Omni.TextEdit
            {
                NewText = textEdit.NewText,
                Range = textEdit.Range.AsOmniSharpRange(),
            };
        }
    }
}
