// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorLSPOptions : IEquatable<RazorLSPOptions>
    {
        public RazorLSPOptions(
            Trace trace,
            bool enableFormatting,
            bool autoClosingTags,
            bool insertSpaces,
            int tabSize,
            bool showLineNumbers,
            bool showHorizontalScrollBar,
            bool showVerticalScrollBar)
        {
            Trace = trace;
            EnableFormatting = enableFormatting;
            AutoClosingTags = autoClosingTags;
            TabSize = tabSize;
            InsertSpaces = insertSpaces;
            ShowLineNumbers = showLineNumbers;
            ShowHorizontalScrollBar = showHorizontalScrollBar;
            ShowVerticalScrollBar = showVerticalScrollBar;
        }

        public static RazorLSPOptions Default =>
            new(trace: default,
                enableFormatting: true,
                autoClosingTags: true,
                insertSpaces: true,
                tabSize: 4,
                showLineNumbers: true,
                showHorizontalScrollBar: true,
                showVerticalScrollBar: true);

        public Trace Trace { get; }

        public LogLevel MinLogLevel => GetLogLevelForTrace(Trace);

        public bool EnableFormatting { get; }

        public bool AutoClosingTags { get; }

        public int TabSize { get; }

        public bool InsertSpaces { get; }

        public bool ShowLineNumbers { get; }

        public bool ShowHorizontalScrollBar { get; }

        public bool ShowVerticalScrollBar { get; }

        public static LogLevel GetLogLevelForTrace(Trace trace)
        {
            return trace switch
            {
                Trace.Off => LogLevel.None,
                Trace.Messages => LogLevel.Information,
                Trace.Verbose => LogLevel.Trace,
                _ => LogLevel.None,
            };
        }

        public bool Equals(RazorLSPOptions other)
        {
            return
                other != null &&
                Trace == other.Trace &&
                EnableFormatting == other.EnableFormatting &&
                AutoClosingTags == other.AutoClosingTags &&
                InsertSpaces == other.InsertSpaces &&
                TabSize == other.TabSize &&
                ShowLineNumbers == other.ShowLineNumbers &&
                ShowHorizontalScrollBar == other.ShowHorizontalScrollBar &&
                ShowVerticalScrollBar == other.ShowVerticalScrollBar;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RazorLSPOptions);
        }

        public override int GetHashCode()
        {
            var hash = new HashCodeCombiner();
            hash.Add(Trace);
            hash.Add(EnableFormatting);
            hash.Add(AutoClosingTags);
            hash.Add(InsertSpaces);
            hash.Add(TabSize);
            hash.Add(ShowLineNumbers);
            hash.Add(ShowHorizontalScrollBar);
            hash.Add(ShowVerticalScrollBar);
            return hash;
        }
    }
}
