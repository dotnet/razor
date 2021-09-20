// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Internal;

namespace Microsoft.CodeAnalysis.Razor.Editor
{
    public sealed class EditorSettings : IEquatable<EditorSettings>
    {
        public static readonly EditorSettings Default = new(
            indentWithTabs: false,
            indentSize: 4,
            showLineNumbers: true,
            showHorizontalScrollBar: true,
            showVerticalScrollBar: true);

        public EditorSettings(bool indentWithTabs, int indentSize, bool showLineNumbers, bool showHorizontalScrollBar, bool showVerticalScrollBar)
        {
            if (indentSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(indentSize));
            }

            IndentWithTabs = indentWithTabs;
            IndentSize = indentSize;
            ShowLineNumbers = showLineNumbers;
            ShowHorizontalScrollBar = showHorizontalScrollBar;
            ShowVerticalScrollBar = showVerticalScrollBar;
        }

        public bool IndentWithTabs { get; }

        public int IndentSize { get; }

        public bool ShowLineNumbers { get; }

        public bool ShowHorizontalScrollBar { get; }

        public bool ShowVerticalScrollBar { get; }

        public bool Equals(EditorSettings other)
        {
            if (other == null)
            {
                return false;
            }

            return IndentWithTabs == other.IndentWithTabs &&
                IndentSize == other.IndentSize &&
                ShowLineNumbers == other.ShowLineNumbers &&
                ShowHorizontalScrollBar == other.ShowHorizontalScrollBar &&
                ShowVerticalScrollBar == other.ShowVerticalScrollBar;
        }

        public override bool Equals(object other)
        {
            return Equals(other as EditorSettings);
        }

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();
            combiner.Add(IndentWithTabs);
            combiner.Add(IndentSize);
            combiner.Add(ShowLineNumbers);
            combiner.Add(ShowHorizontalScrollBar);
            combiner.Add(ShowVerticalScrollBar);

            return combiner.CombinedHash;
        }
    }
}
