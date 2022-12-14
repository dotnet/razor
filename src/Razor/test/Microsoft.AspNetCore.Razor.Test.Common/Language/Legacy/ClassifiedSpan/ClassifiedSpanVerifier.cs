﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal class ClassifiedSpanVerifier
{
    internal static void Verify(RazorSyntaxTree syntaxTree, string[] baseline)
    {
        using (var writer = new StringWriter())
        {
            var walker = new Walker(writer, syntaxTree, baseline);
            walker.Visit();
            walker.AssertReachedEndOfBaseline();
        }
    }

    private class Walker : ClassifiedSpanWriter
    {
        private readonly string[] _baseline;
        private readonly StringWriter _writer;

        private int _index;

        public Walker(StringWriter writer, RazorSyntaxTree syntaxTree, string[] baseline)
            : base(writer, syntaxTree)
        {
            _writer = writer;
            _baseline = baseline;
        }

        public override void VisitClassifiedSpan(ClassifiedSpanInternal span)
        {
            var expected = _index < _baseline.Length ? _baseline[_index++] : null;

            _writer.GetStringBuilder().Clear();
            base.VisitClassifiedSpan(span);
            var actual = _writer.GetStringBuilder().ToString();
            AssertEqual(span, expected, actual);
        }

        public void AssertReachedEndOfBaseline()
        {
            // Since we're walking the list of classified spans there's the chance that our baseline is longer.
            Assert.True(_baseline.Length == _index, $"Not all lines of the baseline were visited! {_baseline.Length} {_index}");
        }

        private void AssertEqual(ClassifiedSpanInternal span, string expected, string actual)
        {
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                return;
            }

            if (expected is null)
            {
                var message = "The span is missing from baseline.";
                throw new ClassifiedSpanBaselineException(span, expected, actual, message);
            }
            else
            {
                var message = $"Contents are not equal.";
                throw new ClassifiedSpanBaselineException(span, expected, actual, message);
            }
        }

        private class ClassifiedSpanBaselineException : XunitException
        {
            public ClassifiedSpanBaselineException(ClassifiedSpanInternal span, string expected, string actual, string userMessage)
                : base(Format(expected, actual, userMessage))
            {
                Span = span;
                Expected = expected;
                Actual = actual;
            }

            public ClassifiedSpanInternal Span { get; }

            public string Actual { get; }

            public string Expected { get; }

            private static string Format(string expected, string actual, string userMessage)
            {
                using var _ = StringBuilderPool.GetPooledObject(out var builder);

                builder.AppendLine(userMessage);
                builder.AppendLine();

                if (expected != null)
                {
                    builder.Append("Expected: ");
                    builder.AppendLine(expected);
                }

                if (actual != null)
                {
                    builder.Append("Actual: ");
                    builder.AppendLine(actual);
                }

                return builder.ToString();
            }
        }
    }
}
