﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class LinePositionSpanExtensions
    {
        public static Range ToRange(this LinePositionSpan linePositionSpan)
        {
            var range = new Range
            {
                Start = new Position { Line = linePositionSpan.Start.Line, Character = linePositionSpan.Start.Character },
                End = new Position { Line = linePositionSpan.End.Line, Character = linePositionSpan.End.Character }
            };

            return range;
        }
    }
}
