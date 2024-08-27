﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Test.Common;

internal readonly struct TestCode
{
    public string Text { get; }
    public ImmutableArray<int> Positions { get; }

    private readonly ImmutableDictionary<string, ImmutableArray<TextSpan>> _nameToSpanMap;

    public TestCode(string input, bool treatPositionIndicatorsAsCode = false)
    {
        if (treatPositionIndicatorsAsCode)
        {
            TestFileMarkupParser.GetSpans(input, treatPositionIndicatorsAsCode, out var text, out var nameToSpanMap);

            Text = text;
            Positions = [];
            _nameToSpanMap = nameToSpanMap;
        }
        else
        {
            TestFileMarkupParser.GetPositionsAndSpans(input, out var text, out var positions, out var spans);

            Text = text;
            Positions = positions;
            _nameToSpanMap = spans;
        }
    }

    public int Position
        => Positions.Single();

    public TextSpan Span
        => Spans.Single();

    public ImmutableArray<TextSpan> Spans
        => GetNamedSpans(string.Empty);

    public ImmutableArray<TextSpan> GetNamedSpans(string name)
        => _nameToSpanMap[name];

    public bool TryGetNamedSpans(string name, out ImmutableArray<TextSpan> spans)
        => _nameToSpanMap.TryGetValue(name, out spans);

    public static implicit operator TestCode(string input)
        => new(input);
}
