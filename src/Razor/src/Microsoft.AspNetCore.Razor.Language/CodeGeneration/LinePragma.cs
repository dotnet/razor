// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration
{
    public sealed class LinePragma : IEquatable<LinePragma>
    {
        public LinePragma(int startLineIndex, int lineCount, string filePath)
        {
            StartLineIndex = startLineIndex;
            LineCount = lineCount;
            FilePath = filePath;
        }

        public int StartLineIndex { get; }

        public int EndLineIndex => StartLineIndex + LineCount;

        public int LineCount { get; }

        public string FilePath { get; }

        public override bool Equals(object obj)
        {
            var other = obj as LinePragma;
            return Equals(other);
        }

        public bool Equals(LinePragma other)
        {
            if (other is null)
            {
                return false;
            }

            return StartLineIndex == other.StartLineIndex &&
                LineCount == other.LineCount &&
                string.Equals(FilePath, other.FilePath, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            var hashCodeCombiner = HashCodeCombiner.Start();
            hashCodeCombiner.Add(StartLineIndex);
            hashCodeCombiner.Add(LineCount);
            hashCodeCombiner.Add(FilePath);

            return hashCodeCombiner;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture, "Line {0}, Count {1} - {2}", StartLineIndex, LineCount, FilePath);
        }
    }
}
