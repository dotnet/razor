// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions
{
    internal static class PositionExtensions
    {
        public static int GetAbsoluteIndex(this Position position, SourceText sourceText)
        {
            if (position is null)
            {
                throw new ArgumentNullException(nameof(position));
            }

            if (sourceText is null)
            {
                throw new ArgumentNullException(nameof(sourceText));
            }

            var linePosition = new LinePosition(position.Line, position.Character);
            if (linePosition.Line >= sourceText.Lines.Count)
            {
                throw new ArgumentOutOfRangeException("Test");
            }
            var index = sourceText.Lines.GetPosition(linePosition);
            return index;
        }
    }
}
