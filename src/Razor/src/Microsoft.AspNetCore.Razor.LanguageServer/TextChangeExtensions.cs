// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal static class TextChangeExtensions
    {
        public static TextEdit AsTextEdit(this TextChange textChange, SourceText sourceText)
        {
            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            var lineSpan = sourceText.Lines.GetLinePositionSpan(textChange.Span);
            var range = new Range(
                new Position(lineSpan.Start.Line, lineSpan.Start.Character),
                new Position(lineSpan.End.Line, lineSpan.End.Character));

            return new TextEdit()
            {
                NewText = textChange.NewText,
                Range = range
            };
        }
    }
}
